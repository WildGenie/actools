﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using AcManager.Pages.Miscellaneous;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Lists;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Attached;
using JetBrains.Annotations;

namespace AcManager.Pages.SelectionLists {
    public abstract class SelectionList<TObject, TItem> : ListBox, ISelectedItemPage<AcObjectNew>, IEqualityComparer<TItem>, IComparer<TItem>
            where TObject : AcObjectNew
            where TItem : SelectCategoryBase {
        private readonly string _cacheKey;
        private readonly string _scrollKey;
        private readonly BaseAcManager<TObject> _manager;
        private readonly IAcWrapperObservableCollection _baseCollection;
        private readonly BetterObservableCollection<TItem> _items;

        protected SelectionList([NotNull] BaseAcManager<TObject> manager, bool isCacheable) {
            if (manager == null) throw new ArgumentNullException(nameof(manager));

            TextSearch.SetTextPath(this, nameof(SelectCategoryBase.DisplayName));
            ScrollViewer.SetHorizontalScrollBarVisibility(this, ScrollBarVisibility.Disabled);
            ScrollViewer.SetCanContentScroll(this, true);

            var baseLey = $@".SelectionList:{typeof(TObject).FullName}:{typeof(TItem).FullName}";
            _scrollKey = $@"{baseLey}:Scroll";
            _cacheKey = isCacheable ? $@"{baseLey}:Items" : null;
            _manager = manager;
            _baseCollection = manager.WrappersList;
            _items = new BetterObservableCollection<TItem>(Rebuild());

            if (!manager.IsLoaded) {
                EnsureLoaded(isCacheable).Forget();
            }

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            PreviewKeyDown += OnPreviewKeyDown;
        }

        protected virtual ICollectionView GetCollectionView(BetterObservableCollection<TItem> items) {
            return (ListCollectionView)CollectionViewSource.GetDefaultView(items);
        }

        private bool _itemsSourceSet;

        private void EnsureItemsSourceSet() {
            if (_itemsSourceSet) return;
            _itemsSourceSet = true;
            ItemsSource = GetCollectionView(_items);
        }

        private async Task EnsureLoaded(bool isCacheable) {
            await _manager.EnsureLoadedAsync();
            if (!isCacheable) {
                SyncSelected();
            } else if (!UpdateIfNeeded()) {
                SaveListToCache(_items);
                SyncSelected();
            }
        }

        private ScrollViewer _scrollViewer;

        public override void OnApplyTemplate() {
            if (_scrollViewer != null) {
                _scrollViewer.ScrollChanged -= OnScrollChanged;
            }

            base.OnApplyTemplate();
            _scrollViewer = GetTemplateChild(@"PART_ScrollViewer") as ScrollViewer;

            if (_scrollViewer != null) {
                _scrollViewer.ScrollChanged += OnScrollChanged;
                LoadScroll();
            }
        }

        private void LoadScroll() {
            EnsureItemsSourceSet();
            _scrollViewer?.ScrollToVerticalOffset(ValuesStorage.GetDouble(_scrollKey));
        }

        private void OnScrollChanged(object sender, ScrollChangedEventArgs e) {
            if (_scrollViewer == null) return;
            ValuesStorage.Set(_scrollKey, _scrollViewer.VerticalOffset);
        }

        protected void OnPanelSizeChanged(object sender, SizeChangedEventArgs e) {
            Dispatcher.BeginInvoke(DispatcherPriority.Render, (Action)LoadScroll);
        }

        private void SaveListToCache(IList<TItem> list) {
            if (_cacheKey == null) return;
            CacheStorage.Set(_cacheKey, list.Select(x => x.Serialize()));
        }

        [CanBeNull]
        private List<TItem> LoadFromCache() {
            if (_cacheKey == null) return null;

            var list = CacheStorage.GetStringList(_cacheKey);
            var result = new List<TItem>((list as IReadOnlyList<string>)?.Count ?? 50);
            foreach (var obj in list) {
                var item = LoadFromCache(obj);
                if (item != null) {
                    result.AddSorted(item, this);
                }
            }

            result.Capacity = result.Count;
            return result;
        }

        [CanBeNull]
        protected abstract TItem LoadFromCache([NotNull] string serialized);

        protected override void OnSelectionChanged(SelectionChangedEventArgs e) {
            base.OnSelectionChanged(e);
            ((UIElement)ItemContainerGenerator.ContainerFromItem(SelectedItem))?.Focus();
        }

        private void SetSelectedItem(TItem item) {
            EnsureItemsSourceSet();
            SelectedItem = item;
            ItemContainerGenerator.StatusChanged += OnItemContainerGeneratorStatusChanged;
        }

        private void OnItemContainerGeneratorStatusChanged(object sender, EventArgs e) {
            if (ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated) {
                ItemContainerGenerator.StatusChanged -= OnItemContainerGeneratorStatusChanged;
                Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => {
                    ((UIElement)ItemContainerGenerator.ContainerFromItem(SelectedItem))?.Focus();
                }));
            }
        }

        private AcObjectNew _selectedAcItem;

        AcObjectNew ISelectedItemPage<AcObjectNew>.SelectedItem {
            get { return _selectedAcItem; }
            set {
                if (Equals(value, _selectedAcItem)) return;
                _selectedAcItem = value;

                SyncSelected(true);
                OnPropertyChanged();
            }
        }

        private bool _outOfSync;

        private void SyncSelected(bool force = false) {
            if (_outOfSync || force) {
                EnsureItemsSourceSet();

                if (!_loaded) {
                    _outOfSync = true;
                    return;
                }

                var item = GetSelectedItem(_items, _selectedAcItem as TObject);
                if (item == null) {
                    _outOfSync = true;
                } else {
                    _outOfSync = false;
                    SetSelectedItem(item);
                }
            }
        }
        
        protected abstract void AddNewIfMissing([NotNull] IList<TItem> list, [NotNull] TObject obj);

        [CanBeNull]
        protected abstract TItem GetSelectedItem([NotNull] IList<TItem> list, [CanBeNull] TObject selected);

        protected void IncreaseCounter([NotNull] TObject obj, [NotNull] TItem item) {
            item.ItemsCount++;
            if ((obj as AcCommonObject)?.IsNew == true) {
                item.IsNew = true;
            }
        }

        protected void AddNewIfMissing([NotNull] IList<TItem> list, [NotNull] TObject obj, [NotNull] TItem item) {
            for (var i = list.Count - 1; i >= 0; i--) {
                var existing = list[i];
                if (existing.IsSameAs(item)) {
                    IncreaseCounter(obj, existing);
                    return;
                }
            }

            list.AddSorted(item, this);
            IncreaseCounter(obj, item);

            if (ReferenceEquals(list, _items)) {
                SyncSelected();
            }
        }

        private IList<TItem> Rebuild(int defaultSize = 100) {
            List<TItem> result;
            if (_cacheKey != null && !_manager.IsLoaded) {
                result = LoadFromCache() ?? new List<TItem>(defaultSize);
            } else {
                result = new List<TItem>(defaultSize);
            }

            foreach (var obj in _baseCollection) {
                if (!obj.IsLoaded) continue;

                var value = obj.Value;
                if (value.Enabled) {
                    AddNewIfMissing(result, (TObject)value);
                }
            }

            result.Capacity = result.Count;
            return result;
        }

        protected bool UpdateIfNeeded() {
            var newList = Rebuild(_items.Count + 1);
            if (_items.ReplaceIfDifferBy(newList, this)) {
                SaveListToCache(_items);
                SyncSelected(true);
                return true;
            }

            var c = newList.Count;
            for (var i = 0; i < c; i++) {
                var n = newList[i];
                var o = _items[i];
                o.ItemsCount = n.ItemsCount;
                o.IsNew = n.IsNew;
            }
            return false;
        }

        private bool _loaded;

        protected virtual void OnLoaded(object sender, RoutedEventArgs e) {
            if (_loaded) return;
            _loaded = true;

            EnsureItemsSourceSet();
            Focus();
            if (!UpdateIfNeeded()) {
                SyncSelected(true);
            }

            _baseCollection.WrappedValueChanged += WrappersList_WrappedValueChanged;
            _baseCollection.CollectionChanged += WrappersList_CollectionChanged;
            _baseCollection.ItemPropertyChanged += WrappersList_ItemPropertyChanged;
        }

        protected virtual void OnUnloaded(object sender, RoutedEventArgs e) {
            if (!_loaded) return;
            _loaded = false;

            _baseCollection.WrappedValueChanged -= WrappersList_WrappedValueChanged;
            _baseCollection.CollectionChanged -= WrappersList_CollectionChanged;
            _baseCollection.ItemPropertyChanged -= WrappersList_ItemPropertyChanged;
        }
        
        protected abstract bool OnObjectPropertyChanged([NotNull] TObject obj, [NotNull] PropertyChangedEventArgs e);

        private void WrappersList_ItemPropertyChanged(object sender, PropertyChangedEventArgs e) {
            var obj = (TObject)sender;
            if (obj.Enabled && OnObjectPropertyChanged(obj, e)) {
                UpdateIfNeeded();
            }
        }

        private void WrappersList_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
            UpdateIfNeeded();
        }

        private void WrappersList_WrappedValueChanged(object sender, WrappedValueChangedEventArgs args) {
            var newValue = args.NewValue as TObject;
            var oldValue = args.OldValue as TObject;

            if (newValue != null) {
                if (oldValue != null) {
                    UpdateIfNeeded();
                } else if (newValue.Enabled) {
                    AddNewIfMissing(_items, newValue);
                }
            } else if (oldValue != null) {
                UpdateIfNeeded();
            }
        }

        [NotNull]
        protected abstract Uri GetPageAddress([NotNull] TItem category);

        private void OnPreviewKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                e.Handled = true;

                var selected = SelectedItem as TItem;
                if (selected != null) {
                    NavigationCommands.GoToPage.Execute(GetPageAddress(selected), (IInputElement)sender);
                }
            }
        }

        protected void OnItemMouseDown(object sender, MouseButtonEventArgs e) {
            e.Handled = true;

            var selected = ((FrameworkElement)sender).DataContext as TItem;
            if (selected != null) {
                NavigationCommands.GoToPage.Execute(GetPageAddress(selected), (IInputElement)sender);
            }
        }

        bool IEqualityComparer<TItem>.Equals(TItem x, TItem y) {
            return x.IsSameAs(y);
        }

        int IEqualityComparer<TItem>.GetHashCode(TItem obj) {
            return obj.GetHashCode();
        }

        int IComparer<TItem>.Compare(TItem x, TItem y) {
            return string.Compare(x.DisplayName, y.DisplayName, StringComparison.Ordinal);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}