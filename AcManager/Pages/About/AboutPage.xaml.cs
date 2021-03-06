﻿using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AcManager.Controls.Dialogs;
using AcManager.Controls.Helpers;
using AcManager.Tools.Helpers;
using AcManager.Tools.Miscellaneous;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;

namespace AcManager.Pages.About {
    public partial class AboutPage {
        private int _clicks;

        public AboutPage() {
            DataContext = new ViewModel();
            InitializeComponent();
        }

        private void Version_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e) {
            if (!SettingsHolder.Common.DeveloperMode && ++_clicks == 10 &&
                    ModernDialog.ShowMessage(AppStrings.About_DeveloperMode, AppStrings.About_DeveloperMode_Title, MessageBoxButton.YesNo) ==
                            MessageBoxResult.Yes) {
                SettingsHolder.Common.DeveloperMode = true;
            }
        }

        private void ContentElement_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            if (Keyboard.Modifiers != (ModifierKeys.Alt | ModifierKeys.Control)) _clicks += 11;
            if (!SettingsHolder.Common.MsMode && (_clicks += 99) == 990 &&
                    ModernDialog.ShowMessage(@"Enable most secret mode? Using it might cause data corruption.", @"Most Secret Mode", MessageBoxButton.YesNo) ==
                            MessageBoxResult.Yes) {
                SettingsHolder.Common.MsMode = true;
            }
        }

        public class ViewModel : NotifyPropertyChanged {
            private ICommand _moreInformationCommand;

            public ICommand MoreInformationCommand => _moreInformationCommand ?? (_moreInformationCommand = new DelegateCommand(() => {
                WindowsHelper.ViewInBrowser("http://acstuff.ru/app");
            }));

            private ICommand _recentChangesCommand;

            public ICommand RecentChangesCommand => _recentChangesCommand ?? (_recentChangesCommand = new DelegateCommand(() => {
                WindowsHelper.ViewInBrowser("http://acstuff.ru/f/d/10-content-manager-detailed-changelog");
            }));

            private const string KeyLogsSentTime = "GeneralViewModel.KeyLogsSentTime";

            private ICommand _sendLogsCommand;

            public ICommand SendLogsCommand => _sendLogsCommand ?? (_sendLogsCommand = new AsyncCommand(async () => {
                try {
                    var message = Prompt.Show(
                            AppStrings.About_ReportAnIssue_Prompt,
                            AppStrings.About_ReportAnIssue_Title, watermark: @"?", multiline: true);
                    if (message == null) return;
                    await Task.Run(() => AppReporter.SendLogs(message));
                    ValuesStorage.Set(KeyLogsSentTime, DateTime.Now);
                    Toast.Show(AppStrings.About_ReportAnIssue_Sent, AppStrings.About_ReportAnIssue_Sent_Message);
                } catch (Exception e) {
                    NonfatalError.Notify(AppStrings.About_ReportAnIssue_CannotSend, e);
                }
            }, () => (DateTime.Now - ValuesStorage.GetDateTime(KeyLogsSentTime, default(DateTime))).TotalSeconds > 3d, TimeSpan.FromSeconds(3)));
        }
    }
}
