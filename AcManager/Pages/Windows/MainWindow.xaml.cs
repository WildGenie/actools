﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using AcManager.Controls.Helpers;
using AcManager.Controls.QuickSwitches;
using AcManager.Controls.UserControls;
using AcManager.Controls.ViewModels;
using AcManager.Internal;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Drive;
using AcManager.Pages.Lists;
using AcManager.Tools;
using AcManager.Tools.About;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Managers;
using AcManager.Tools.Managers.Online;
using AcManager.Tools.Managers.Plugins;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Converters;
using FirstFloor.ModernUI.Windows.Media;
using FirstFloor.ModernUI.Windows.Navigation;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Application = System.Windows.Application;
using DataFormats = System.Windows.DataFormats;
using DragEventArgs = System.Windows.DragEventArgs;
using DragDropEffects = System.Windows.DragDropEffects;
using Path = System.Windows.Shapes.Path;
using QuickSwitchesBlock = AcManager.QuickSwitches.QuickSwitchesBlock;

namespace AcManager.Pages.Windows {
    public partial class MainWindow : IFancyBackgroundListener {
        public static bool OptionLiteModeSupported = false;

        private readonly bool _cancelled;
        private readonly string _testGameDialog = null;

        public MainWindow() {
            var app = Application.Current;
            if (app != null) {
                app.MainWindow = this;
            }

            _cancelled = false;

            if (AppArguments.Values.Any()) {
                ProcessArguments();
            }

            if (_testGameDialog != null) {
                Logging.Write("Testing mode");
                var ui = new GameDialog();
                ui.ShowDialogWithoutBlocking();
                ((IGameUi)ui).OnResult(JsonConvert.DeserializeObject<Game.Result>(FileUtils.ReadAllText(_testGameDialog)), null);
                _cancelled = true;
            }

            if (_cancelled) {
                Close();
                return;
            }

            DataContext = new ViewModel();
            InputBindings.AddRange(new[] {
                new InputBinding(new NavigateCommand(this, "about"), new KeyGesture(Key.F1, ModifierKeys.Alt)),
                new InputBinding(new NavigateCommand(this, "settings"), new KeyGesture(Key.F1, ModifierKeys.Control)),
                new InputBinding(new NavigateCommand(this, "drive"), new KeyGesture(Key.F1)),
                new InputBinding(new NavigateCommand(this, "lapTimes"), new KeyGesture(Key.F2)),
                new InputBinding(new NavigateCommand(this, "stats"), new KeyGesture(Key.F3)),
                new InputBinding(new NavigateCommand(this, "media"), new KeyGesture(Key.F4))
            });
            InitializeComponent();

            LinkNavigator.Commands.Add(new Uri("cmd://enterkey"), Model.EnterKeyCommand);
            AppKeyHolder.ProceedMainWindow(this);
            
            foreach (var result in MenuLinkGroups.OfType<LinkGroupFilterable>()
                                                 .Where(x => x.Source.OriginalString.Contains(@"/online.xaml", StringComparison.OrdinalIgnoreCase))) {
                result.LinkChanged += OnlineLinkChanged;
            }

            foreach (var result in MenuLinkGroups.OfType<LinkGroupFilterable>()
                                                 .Where(x => x.GroupKey == "content")) {
                result.LinkChanged += ContentLinkChanged;
            }

            UpdateLiveTabs();
            SettingsHolder.Live.PropertyChanged += Live_PropertyChanged;

            UpdateServerTab();
            UpdateMinoratingLink();
            SettingsHolder.Online.PropertyChanged += Online_PropertyChanged;

            if (!OfficialStarterNotification() && PluginsManager.Instance.HasAnyNew()) {
                Toast.Show("Don’t forget to install plugins!", ""); // TODO?
            }

            EntryPoint.HandleSecondInstanceMessages(this, HandleMessagesAsync);

            _defaultOnlineGroupCount = OnlineGroup.FixedLinks.Count;

            if (FileBasedOnlineSources.IsInitialized()) {
                UpdateOnlineSourcesLinks();
            }
            FileBasedOnlineSources.Instance.Update += OnOnlineSourcesUpdate;

            Activated += OnActivated;

#if DEBUG
            LapTimesGrid.Source = new Uri("/Pages/Miscellaneous/LapTimes_Grid.xaml", UriKind.Relative);
#endif
        }

        private void OnActivated(object sender, EventArgs e) {
            Activated -= OnActivated;

            var app = Application.Current;
            if (app == null) return;
            foreach (var dialog in app.Windows.OfType<ModernDialog>().Where(x => x.Owner == null)) {
                dialog.Owner = this;
            }
        }

        private readonly int _defaultOnlineGroupCount;

        private void UpdateOnlineSourcesLinks() {
            var list = OnlineGroup.FixedLinks;

            for (var i = list.Count - 1; i >= _defaultOnlineGroupCount; i--) {
                list.RemoveAt(i);
            }

            foreach (var source in FileBasedOnlineSources.Instance.GetVisibleSources().OrderBy(x => x.DisplayName)) {
                list.Add(new Link {
                    DisplayName = $@"{source.DisplayName}",
                    Source = UriExtension.Create("/Pages/Drive/Online.xaml?Filter=@{0}&Special=1", source.Id)
                });
            }
        }

        private void OnOnlineSourcesUpdate(object sender, EventArgs e) {
            UpdateOnlineSourcesLinks();
        }

        private async void HandleMessagesAsync(IEnumerable<string> data) {
            await Task.Delay(1);
            foreach (var argument in data) {
                await _argumentsHandler.ProcessArgument(argument);
                await Task.Delay(1);
            }
        }

        private void Online_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(SettingsHolder.OnlineSettings.ServerPresetsManaging):
                    UpdateServerTab();
                    break;
                case nameof(SettingsHolder.OnlineSettings.IntegrateMinorating):
                    UpdateMinoratingLink();
                    break;
            }
        }

        private void UpdateServerTab() {
            ServerGroup.IsShown = SettingsHolder.Online.ServerPresetsManaging;
        }

        private void UpdateMinoratingLink() {
            MinoratingLink.IsShown = SettingsHolder.Online.IntegrateMinorating;
        }

        private const string KeyOfficialStarterNotification = "mw.osn";

        private bool OfficialStarterNotification() {
            if (ValuesStorage.GetBool(KeyOfficialStarterNotification)) return false;

            if (SettingsHolder.Drive.SelectedStarterType == SettingsHolder.DriveSettings.OfficialStarterType) {
                ValuesStorage.Set(KeyOfficialStarterNotification, true);
                return false;
            }

            var launcher = FileUtils.GetAcLauncherFilename(AcRootDirectory.Instance.RequireValue);
            if (FileVersionInfo.GetVersionInfo(launcher).FileVersion.IsVersionOlderThan(@"0.16.714")) {
                return false;
            }

            Toast.Show(AppStrings.Main_OfficialSupportNotification, AppStrings.Main_OfficialSupportNotification_Details, () => {
                if (ModernDialog.ShowMessage(
                        AppStrings.Main_OfficialSupportNotification_Message,
                        Controls.ControlsStrings.Common_GoodNews, MessageBoxButton.YesNo) == MessageBoxResult.Yes) {
                    SettingsHolder.Drive.SelectedStarterType = SettingsHolder.DriveSettings.OfficialStarterType;
                }

                ValuesStorage.Set(KeyOfficialStarterNotification, true);
            });
            return true;
        }

        private void Live_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(SettingsHolder.LiveSettings.RsrEnabled) ||
                    e.PropertyName == nameof(SettingsHolder.LiveSettings.SrsEnabled)) {
                UpdateLiveTabs();
            }
        }

        private void UpdateLiveTabs() {
            RsrLink.IsShown = SettingsHolder.Live.RsrEnabled;
            SrsLink.IsShown = SettingsHolder.Live.SrsEnabled;
            LiveGroup.IsShown = LiveGroup.Links.Any(x => x.IsShown);
        }

        /// <summary>
        /// Temporary (?) fix.
        /// </summary>
        private static void OnlineLinkChanged(object sender, LinkChangedEventArgs e) {
            Online.OnLinkChanged(e);
        }

        /// <summary>
        /// Temporary (?) fix to keep selected object while editing current filter.
        /// </summary>
        private static void ContentLinkChanged(object sender, LinkChangedEventArgs e) {
            switch (((LinkGroupFilterable)sender).DisplayName) {
                case "cars":
                    AcListPageViewModel<CarObject>.OnLinkChanged(e);
                    break;
                case "tracks":
                    AcListPageViewModel<TrackObject>.OnLinkChanged(e);
                    break;
                case "showrooms":
                    AcListPageViewModel<ShowroomObject>.OnLinkChanged(e);
                    break;
            }
        }

        private class NavigateCommand : CommandExt {
            private readonly MainWindow _window;
            private readonly string _key;

            public NavigateCommand(MainWindow window, [Localizable(false)] string key) : base(true, false) {
                _window = window;
                _key = key;
            }

            protected override bool CanExecuteOverride() {
                return true;
            }

            protected override void ExecuteOverride() {
                var link = _window.TitleLinks.OfType<TitleLink>().FirstOrDefault(x => x.GroupKey == _key);
                if (link == null || !link.IsEnabled || link.NonSelectable) return;
                _window.NavigateTo(link.Source);
            }
        }

        private readonly ArgumentsHandler _argumentsHandler = new ArgumentsHandler();

        private async void ProcessArguments() {
            if (OptionLiteModeSupported) {
                Visibility = Visibility.Hidden;
            }

            var cancelled = true;
            foreach (var arg in AppArguments.Values) {
                Logging.Write("Input: " + arg);

                var result = await _argumentsHandler.ProcessArgument(arg);
                if (result == ArgumentHandleResult.FailedShow) {
                    NonfatalError.Notify(AppStrings.Main_CannotProcessArgument, AppStrings.Main_CannotProcessArgument_Commentary);
                }

                if (result == ArgumentHandleResult.SuccessfulShow || result == ArgumentHandleResult.FailedShow) {
                    Visibility = Visibility.Visible;
                    cancelled = false;
                }
            }

            if (OptionLiteModeSupported && cancelled) {
                Close();
            }
        }

        public new void Show() {
            if (_cancelled) {
                Logging.Write("Cancelled");
                return;
            }

            base.Show();
        }

        private ViewModel Model => (ViewModel)DataContext;

        public class ViewModel : NotifyPropertyChanged {
            private CommandBase _enterKeyCommand;

            public ICommand EnterKeyCommand => _enterKeyCommand ?? (_enterKeyCommand = new DelegateCommand(() => {
                new AppKeyDialog().ShowDialog();
            }));

            public AppUpdater AppUpdater => AppUpdater.Instance;
        }

        void IFancyBackgroundListener.ChangeBackground(string filename) {
            if (_dynamicBackground != null) return;
            var backgroundContent = BackgroundContent;
            FancyBackgroundManager.UpdateBackground(this, ref backgroundContent);
            if (!ReferenceEquals(backgroundContent, BackgroundContent)) {
                BackgroundContent = backgroundContent;
            }
        }

        private HwndSourceHook _hook;
        private bool _loaded;

        private void OnLoaded(object sender, RoutedEventArgs e) {
            if (_loaded) return;
            _loaded = true;

            AboutHelper.Instance.PropertyChanged += About_PropertyChanged;
            UpdateAboutIsNew();

            var background = AppArguments.Get(AppFlag.Background);
            if (string.IsNullOrWhiteSpace(background)) {
                FancyBackgroundManager.Instance.AddListener(this);
                SetThemeDynamicBackgroundListener();
            } else {
                background = FileUtils.GetFullPath(background, () => FilesStorage.Instance.GetDirectory("Themes", "Backgrounds"));
                ApplyDynamicBackground(background, AppArguments.GetDouble(AppFlag.BackgroundOpacity, 0.5));
            }
        }

        private DynamicBackground _dynamicBackground;

        private void ApplyDynamicBackground([CanBeNull] string filename, double opacity = 0.5) {
            ActionExtension.InvokeInMainThreadAsync(() => {
                try {
                    if (filename == null) {
                        DisposeHelper.Dispose(ref _dynamicBackground);
                        if (FancyBackgroundManager.Instance.Enabled) {
                            FancyBackgroundManager.Instance.Recreate(this);
                        } else {
                            ClearValue(BackgroundContentProperty);
                        }
                    } else {
                        var animatedBackground = Regex.IsMatch(filename, @"\.(?:avi|flv|gif|m(?:4[pv]|kv|ov|p[4g])|og[vg]|qt|webm|wmv)$", RegexOptions.IgnoreCase) ?
                                filename : null;
                        var staticBackground = animatedBackground == null ? filename : Regex.Replace(filename, @"\.\w+$", @".jpg");

                        _dynamicBackground?.Dispose();
                        BackgroundContent = _dynamicBackground = new DynamicBackground {
                            Animated = animatedBackground,
                            Static = staticBackground,
                            Opacity = opacity
                        };
                    }
                } catch (Exception e) {
                    Logging.Error(e);
                }
            });
        }

        private void UpdateThemeDynamicBackground() {
            var value = AppearanceManager.Current.CurrentThemeDictionary?[@"DynamicBackground"] as string;
            if (value != null) {
                value = FileUtils.GetFullPath(value, () => FilesStorage.Instance.GetDirectory());
                ApplyDynamicBackground(value, AppearanceManager.Current.CurrentThemeDictionary?[@"DynamicBackgroundOpacity"] as double? ?? 0.5);
            } else {
                ApplyDynamicBackground(null);
            }
        }

        private void SetThemeDynamicBackgroundListener() {
            UpdateThemeDynamicBackground();
            AppearanceManager.Current.PropertyChanged += (sender, args) => {
                if (args.PropertyName == nameof(AppearanceManager.CurrentThemeDictionary)) {
                    ActionExtension.InvokeInMainThreadAsync(UpdateThemeDynamicBackground);
                }
            };
        }

        private void UpdateAboutIsNew() {
            TitleLinks.FirstOrDefault(x => x.DisplayName == AppStrings.Main_About)?
                      .SetNew(AboutHelper.Instance.HasNewImportantTips || AboutHelper.Instance.HasNewReleaseNotes);
            MenuLinkGroups.SelectMany(x => x.Links)
                          .FirstOrDefault(x => x.DisplayName == AppStrings.Main_ReleaseNotes)?
                          .SetNew(AboutHelper.Instance.HasNewReleaseNotes);
            MenuLinkGroups.SelectMany(x => x.Links)
                          .FirstOrDefault(x => x.DisplayName == AppStrings.Main_ImportantTips)?
                          .SetNew(AboutHelper.Instance.HasNewImportantTips);
        }

        private void About_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(AboutHelper.HasNewReleaseNotes) || e.PropertyName == nameof(AboutHelper.HasNewImportantTips)) {
                UpdateAboutIsNew();
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            if (!_loaded) return;
            _loaded = false;

            FancyBackgroundManager.Instance.RemoveListener(this);
            AboutHelper.Instance.PropertyChanged -= About_PropertyChanged;

            if (_hook == null) return;

            try {
                HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.RemoveHook(_hook);
            } catch (Exception) {
                Logging.Warning("Can’t remove one-instance hook");
            }

            _hook = null;
        }

        private void OnDrop(object sender, DragEventArgs e) {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop) && !e.Data.GetDataPresent(DataFormats.UnicodeText)) return;

            Focus();

            var data = e.Data.GetData(DataFormats.FileDrop) as string[] ??
                       (e.Data.GetData(DataFormats.UnicodeText) as string)?.Split('\n')
                                                                           .Select(x => x.Trim())
                                                                           .Select(x => x.Length > 1 && x.StartsWith(@"""") && x.EndsWith(@"""")
                                                                                   ? x.Substring(1, x.Length - 2) : x);
            Dispatcher.InvokeAsync(() => ProcessDroppedFiles(data));
        }

        private void OnDragEnter(object sender, DragEventArgs e) {
            if (e.AllowedEffects.HasFlag(DragDropEffects.All) &&
                    (e.Data.GetDataPresent(DataFormats.FileDrop) || e.Data.GetDataPresent(DataFormats.UnicodeText))) {
                e.Effects = DragDropEffects.All;
            }
        }

        private async void ProcessDroppedFiles(IEnumerable<string> files) {
            if (files == null) return;
            foreach (var filename in files) {
                await _argumentsHandler.ProcessArgument(filename);
            }
        }

        private void OnClosed(object sender, EventArgs e) {
            _dynamicBackground?.Dispose();
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) {
                WindowsHelper.RestartCurrentApplication();
            } else {
                var app = Application.Current;
                if (app == null) {
                    Environment.Exit(0);
                } else {
                    app.Shutdown();
                }
            }
        }

        private void OnClosing(object sender, CancelEventArgs e) {
            if (SettingsHolder.Online.ServerPresetsManaging && ServerPresetsManager.Instance.IsScanned) {
                var running = ServerPresetsManager.Instance.LoadedOnly.Where(x => x.IsRunning).ToList();
                if (running.Count > 0 && ModernDialog.ShowMessage(
                        $@"{"If you’ll close app, running servers will be stopped as well. Are you sure?"}{Environment.NewLine}{Environment.NewLine}{
                                running.Select(x => $@" • {x.DisplayName}").JoinToString(Environment.NewLine)}",
                        "Some servers are running", MessageBoxButton.OKCancel) != MessageBoxResult.OK) {
                    e.Cancel = true;
                    return;
                }
            }

            var unsaved = Superintendent.Instance.UnsavedChanges();
            if (unsaved.Count > 0) {
                switch (ModernDialog.ShowMessage(
                        $@"{AppStrings.Main_UnsavedChanges}{Environment.NewLine}{Environment.NewLine}{
                                unsaved.Select(x => $@" • {x}").JoinToString(Environment.NewLine)}",
                        AppStrings.Main_UnsavedChangesHeader, MessageBoxButton.YesNoCancel)) {
                            case MessageBoxResult.Yes:
                                Superintendent.Instance.SaveAll();
                                break;
                            case MessageBoxResult.Cancel:
                                e.Cancel = true;
                                break;
                }
            }
        }

        private void InitializePopup() {
            if (Popup.Child == null) {
                Popup.Child = new QuickSwitchesBlock();
            }
        }

        public IEnumerable<FrameworkElement> GetQuickSwitches() {
            InitializePopup();
            return ((QuickSwitchesBlock)Popup.Child).Items;
        } 

        private void ToggleQuickSwitches(bool force = true) {
            if (Popup.IsOpen) {
                Popup.IsOpen = false;
            } else if (force || _openOnNext) {
                InitializePopup();
                Popup.IsOpen = true;
                Popup.Focus();
            }
        }

        private ICommand _quickSwitchesCommand;

        public ICommand QuickSwitchesCommand => _quickSwitchesCommand ?? (_quickSwitchesCommand = new DelegateCommand(() => {
            ToggleQuickSwitches();
        }));

        private int _popupId;

        private async void ShowQuickSwitchesPopup(Geometry icon, string message, object toolTip) {
            if (Popup.IsOpen) return;

            var id = ++_popupId;
            QuickSwitchesNotificationIcon.Data = icon;
            QuickSwitchesNotificationText.Text = message?.ToUpper(CultureInfo.CurrentUICulture);
            QuickSwitchesNotification.IsOpen = true;
            QuickSwitchesNotification.ToolTip = toolTip;

            await Task.Delay(2000);

            if (_popupId == id) {
                QuickSwitchesNotification.IsOpen = false;
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e) {
            if (Keyboard.Modifiers != ModifierKeys.Alt && Keyboard.Modifiers != (ModifierKeys.Alt | ModifierKeys.Shift) ||
                    !SettingsHolder.Drive.QuickSwitches) return;
           
            switch (e.SystemKey) {
                case Key.OemTilde:
                    ToggleQuickSwitches();
                    break;
                    
                default:
                    var k = e.SystemKey - Key.D1;
                    if (k < 0 || k > 9) return;

                    InitializePopup();
                    var child = GetQuickSwitches().ElementAtOrDefault(k);
                    if (child == null) break;

                    QuickSwitchesNotification.SetValue(TextBlock.ForegroundProperty, child.GetValue(TextBlock.ForegroundProperty));

                    var toggle = child as ModernToggleButton;
                    if (toggle != null) {
                        toggle.IsChecked = !toggle.IsChecked;
                        ShowQuickSwitchesPopup(toggle.IconData, $@"{toggle.Content}: {toggle.IsChecked.ToReadableBoolean()}", child.ToolTip);
                        break;
                    }

                    var button = child as ModernButton;
                    if (button != null) {
                        button.Command?.Execute(null);
                        ShowQuickSwitchesPopup(button.IconData, button.Content?.ToString(), child.ToolTip);
                        break;
                    }

                    var presets = child as QuickSwitchPresetsControl;
                    if (presets != null) {
                        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) {
                            presets.SwitchToPrevious();
                        } else {
                            presets.SwitchToNext();
                        }
                        ShowQuickSwitchesPopup(presets.IconData, $@"{presets.CurrentUserPreset.DisplayName}", child.ToolTip);
                        break;
                    }

                    var combo = child as QuickSwitchComboBox;
                    if (combo != null && combo.Items.Count > 1) {
                        var index = combo.SelectedIndex;
                        combo.SelectedItem = combo.Items[(index + (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1) +
                                combo.Items.Count) % combo.Items.Count];
                        ShowQuickSwitchesPopup(combo.IconData, $@"{combo.SelectedItem}", child.ToolTip);
                        break;
                    }

                    var slider = child as QuickSwitchSlider;
                    if (slider != null) {
                        var step = (slider.Maximum - slider.Minimum) / 6d;
                        var position = (((slider.Value - slider.Minimum) / step - 1).Clamp(0, 4).Round(1d) +
                                (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1) + 5) % 5;
                        slider.Value = slider.Minimum + (position + 1) * step;
                        ShowQuickSwitchesPopup(slider.IconData, $@"{slider.Content}: {slider.DisplayValue}", child.ToolTip);
                    }

                    // special case for controls presets
                    var dock = child as DockPanel;
                    if (dock != null) {
                        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) {
                            ControlsPresets.Instance.SwitchToPrevious();
                        } else {
                            ControlsPresets.Instance.SwitchToNext();
                        }

                        ShowQuickSwitchesPopup(dock.FindLogicalChild<Path>()?.Data, $@"{AcSettingsHolder.Controls.CurrentPresetName}", child.ToolTip);
                    }

                    break;
            }

            e.Handled = true;
        }

        private async void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e) {
            if (!SettingsHolder.Drive.QuickSwitches) return;

            await Task.Delay(50);
            if (e.Handled) return;

            ToggleQuickSwitches(false);
            e.Handled = true;
        }

        private void OnPreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e) {
            if (Popup.IsOpen) {
                e.Handled = true;
            }
        }

        private bool _openOnNext;

        private void OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e) {
            _openOnNext = !Popup.IsOpen;
        }

        private class InnerPopupHeightConverter : IValueConverter {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
                return value.AsDouble() / OptionScale - 2d;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
                throw new NotSupportedException();
            }
        }

        public static IValueConverter PopupHeightConverter { get; } = new InnerPopupHeightConverter();

        private void TitleLinkContent_OnDrop(object sender, DragEventArgs e) {
            var trackObject = e.Data.GetData(TrackObjectBase.DraggableFormat) as TrackObjectBase;
            if (trackObject != null) {
                TracksListPage.Show(trackObject);
                e.Effects = DragDropEffects.Copy;
                return;
            }

            var raceGridEntry = e.Data.GetData(RaceGridEntry.DraggableFormat) as RaceGridEntry;
            if (raceGridEntry != null) {
                CarsListPage.Show(raceGridEntry.Car, raceGridEntry.CarSkin?.Id);
                e.Effects = DragDropEffects.Copy;
                return;
            }

            var carObject = e.Data.GetData(CarObject.DraggableFormat) as CarObject;
            if (carObject != null) {
                CarsListPage.Show(carObject);
                e.Effects = DragDropEffects.Copy;
                return;
            }

            e.Effects = DragDropEffects.None;
        }

        private void TitleLinkDrive_OnDrop(object sender, DragEventArgs e) {
            var raceGridEntry = e.Data.GetData(RaceGridEntry.DraggableFormat) as RaceGridEntry;
            var carObject = e.Data.GetData(CarObject.DraggableFormat) as CarObject;

            if (raceGridEntry == null && carObject == null) {
                e.Effects = DragDropEffects.None;
                return;
            }

            QuickDrive.Show(carObject ?? raceGridEntry.Car, raceGridEntry?.CarSkin?.Id);
            e.Effects = DragDropEffects.Copy;
        }

        private void MakeSureOnlineIsReady([CanBeNull] Uri uri) {
            if (uri?.OriginalString.Contains(@"/online.xaml", StringComparison.OrdinalIgnoreCase) == true) {
                OnlineManager.EnsureInitialized();
            }
        }

        private void OnFrameNavigating(object sender, NavigatingCancelEventArgs e) {
            MakeSureOnlineIsReady(e.Source);
        }

        private void OnMainMenuInitialize(object sender, ModernMenu.InitializeEventArgs e) {
            MakeSureOnlineIsReady(e.LoadedUri);
        }
    }
}
