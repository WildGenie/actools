﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Navigation;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Controls {
    public interface ITitleable {
        string Title { get; }
    }

    public enum SavePolicy {
        /// <summary>
        /// Save or load only when URI is in one of original links.
        /// </summary>
        Strict,

        /// <summary>
        /// Save or load even if there is no link with that URI.
        /// </summary>
        Flexible
    }

    public class DirectContentLoaderEntry {
        public Uri Source { get; set; }

        public object Content { get; set; }
    }

    [ContentProperty(nameof(Entries))]
    public class DirectContentLoader : IContentLoader {
        public List<DirectContentLoaderEntry> Entries { get; }

        public DirectContentLoader() {
            Entries = new List<DirectContentLoaderEntry>(3);
        }

        public Task<object> LoadContentAsync(Uri uri, CancellationToken cancellationToken) {
            return Task.FromResult(LoadContent(uri));
        }

        public object LoadContent(Uri uri) {
            return Entries.FirstOrDefault(x => string.Equals(x.Source.ToString(), uri.OriginalString, StringComparison.OrdinalIgnoreCase))?.Content;
        }
    }

    public class ModernTab : Control {
        public static readonly DependencyProperty LinksHorizontalAlignmentProperty = DependencyProperty.Register("LinksHorizontalAlignment", 
            typeof(HorizontalAlignment), typeof(ModernTab), new PropertyMetadata());

        public HorizontalAlignment LinksHorizontalAlignment {
            get { return (HorizontalAlignment)GetValue(LinksHorizontalAlignmentProperty); }
            set { SetValue(LinksHorizontalAlignmentProperty, value); }
        }

        public static readonly DependencyProperty LinksMarginProperty = DependencyProperty.Register("LinksMargin", typeof(Thickness), 
            typeof(ModernTab), new PropertyMetadata(new Thickness(0.0, 0.0, 0.0, 0.0)));

        public static readonly DependencyProperty FrameMarginProperty = DependencyProperty.Register("FrameMargin", typeof(Thickness), 
            typeof(ModernTab), new PropertyMetadata(new Thickness(0.0, 0.0, 0.0, 0.0)));

        public Thickness LinksMargin {
            get { return (Thickness)GetValue(LinksMarginProperty); }
            set { SetValue(LinksMarginProperty, value); }
        }

        public Thickness FrameMargin {
            get { return (Thickness)GetValue(FrameMarginProperty); }
            set { SetValue(FrameMarginProperty, value); }
        }

        public static readonly DependencyProperty SavePolicyProperty = DependencyProperty.Register(nameof(SavePolicy), typeof(SavePolicy),
                typeof(ModernTab));

        public SavePolicy SavePolicy {
            get { return (SavePolicy)GetValue(SavePolicyProperty); }
            set { SetValue(SavePolicyProperty, value); }
        }

        public static readonly DependencyProperty ContentLoaderProperty = DependencyProperty.Register("ContentLoader", typeof(IContentLoader), 
            typeof(ModernTab), new PropertyMetadata(new DefaultContentLoader()));

        public static readonly DependencyProperty LayoutProperty = DependencyProperty.Register("Layout", typeof(TabLayout), 
            typeof(ModernTab), new PropertyMetadata(TabLayout.Tab));

        public static readonly DependencyProperty ListWidthProperty = DependencyProperty.Register("ListWidth", typeof(GridLength), 
            typeof(ModernTab), new PropertyMetadata(new GridLength(170)));

        public static readonly DependencyProperty LinksProperty = DependencyProperty.Register("Links", typeof(LinkCollection),
            typeof(ModernTab), new PropertyMetadata(OnLinksChanged));

        public static readonly DependencyProperty SelectedSourceProperty = DependencyProperty.Register("SelectedSource", typeof(Uri), 
            typeof(ModernTab), new PropertyMetadata(OnSelectedSourceChanged));

        public event EventHandler<SourceEventArgs> SelectedSourceChanged;
        public event EventHandler<NavigationEventArgs> FrameNavigated;

        private ListBox _linkList;

        public ModernTab() {
            DefaultStyleKey = typeof(ModernTab);
            SetCurrentValue(LinksProperty, new LinkCollection());
        }

        public ModernFrame Frame { get; private set; }

        private static void OnLinksChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((ModernTab)o).UpdateSelection(false);
        }

        private static void OnSelectedSourceChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((ModernTab)o).OnSelectedSourceChanged((Uri)e.NewValue);
        }

        private void OnSelectedSourceChanged(Uri newValue) {
            UpdateSelection(true);
            SelectedSourceChanged?.Invoke(this, new SourceEventArgs(newValue));
        }

        private void UpdateSelection(bool skipLoading) {
            if (_linkList == null || Links == null) {
                return;
            }

            if (!skipLoading && SavePolicy == SavePolicy.Flexible && SaveKey != null) {
                Frame.Source = ValuesStorage.GetUri(SaveKey) ?? Links.FirstOrDefault()?.Source;
            } else {
                var saved = skipLoading || SaveKey == null ? null : ValuesStorage.GetString(SaveKey);
                _linkList.SelectedItem = (saved == null ? null : Links.FirstOrDefault(l => l.Source.OriginalString == saved))
                        ?? Links.FirstOrDefault(l => l.Source == SelectedSource) ?? (skipLoading ? null : Links.FirstOrDefault());
            }
        }

        public override void OnApplyTemplate() {
            base.OnApplyTemplate();

            if (_linkList != null) {
                _linkList.SelectionChanged -= OnLinkListSelectionChanged;
            }

            if (Frame != null) {
                Frame.Navigated -= Frame_Navigated;
            }

            _linkList = GetTemplateChild(@"PART_LinkList") as ListBox;
            Frame = GetTemplateChild(@"PART_Frame") as ModernFrame;

            if (_linkList != null) {
                _linkList.SelectionChanged += OnLinkListSelectionChanged;
            }

            if (Frame != null) {
                Frame.Navigated += Frame_Navigated;
            }

            UpdateSelection(false);
        }

        private void OnLinkListSelectionChanged(object sender, SelectionChangedEventArgs e) {
            var link = _linkList.SelectedItem as Link;
            if (link != null && link.Source != SelectedSource) {
                SetCurrentValue(SelectedSourceProperty, link.Source);
            }
        }

        private void Frame_Navigated(object sender, NavigationEventArgs navigationEventArgs) {
            if (Layout == TabLayout.TabWithTitle) {
                Title = (Frame.Content as ITitleable)?.Title;
            }

            FrameNavigated?.Invoke(this, navigationEventArgs);

            if (SaveKey != null && (_linkList.SelectedItem != null || SavePolicy == SavePolicy.Flexible)) {
                ValuesStorage.Set(SaveKey, Frame.Source.OriginalString);
            }
        }

        public IContentLoader ContentLoader {
            get { return (IContentLoader)GetValue(ContentLoaderProperty); }
            set { SetValue(ContentLoaderProperty, value); }
        }

        public TabLayout Layout {
            get { return (TabLayout)GetValue(LayoutProperty); }
            set {
                Title = null;
                SetValue(LayoutProperty, value);
            }
        }

        public LinkCollection Links {
            get { return (LinkCollection)GetValue(LinksProperty); }
            set { SetValue(LinksProperty, value); }
        }

        public GridLength ListWidth {
            get { return (GridLength)GetValue(ListWidthProperty); }
            set { SetValue(ListWidthProperty, value); }
        }

        public Uri SelectedSource {
            get { return (Uri)GetValue(SelectedSourceProperty); }
            set { SetValue(SelectedSourceProperty, value); }
        }

        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof (string),
                                                                                             typeof (ModernTab));

        public string Title {
            get { return (string) GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        // saving and loading uri
        public static readonly DependencyProperty SaveKeyProperty = DependencyProperty.Register(nameof(SaveKey), typeof(string),
                typeof(ModernTab), new PropertyMetadata(OnSaveKeyChanged));

        [CanBeNull]
        public string SaveKey {
            get { return (string)GetValue(SaveKeyProperty); }
            set { SetValue(SaveKeyProperty, value); }
        }

        private static void OnSaveKeyChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((ModernTab)o).UpdateSelection(false);
        }

        public static readonly DependencyProperty LinksListBoxTemplateProperty = DependencyProperty.Register(nameof(LinksListBoxTemplate), typeof(ControlTemplate),
                typeof(ModernTab));

        public ControlTemplate LinksListBoxTemplate {
            get { return (ControlTemplate)GetValue(LinksListBoxTemplateProperty); }
            set { SetValue(LinksListBoxTemplateProperty, value); }
        }
    }

    public class ModernTabDataTemplateSelector : DataTemplateSelector {
        public DataTemplate LinkDataTemplate { get; set; }

        public DataTemplate TitleDataTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container) {
            return item is Link ? LinkDataTemplate : TitleDataTemplate;
        }
    }
}
