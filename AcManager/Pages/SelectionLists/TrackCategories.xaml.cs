﻿using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using AcManager.Tools.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.SelectionLists {
    /// <summary>
    /// Interaction logic for SelectTrackDialog_Categories.xaml
    /// </summary>
    public partial class TrackCategories {
        public ViewModel Model => (ViewModel) DataContext;

        public TrackCategories() {
            DataContext = new ViewModel();
            InitializeComponent();
        }

        private static Uri GetPageAddress(SelectCategoryDescription category) {
            return UriExtension.Create("/Pages/Miscellaneous/AcObjectSelectList.xaml?Type=track&Filter={0}&Title={1}",
                $"enabled+&({category.Filter})", category.Name);
        }

        private void CategoriesListBox_OnPreviewKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                e.Handled = true;

                var selected = CategoriesListBox.SelectedItem as SelectCategoryDescription;
                if (selected == null) return;
                NavigationCommands.GoToPage.Execute(GetPageAddress(selected), (IInputElement)sender);
            }
        }

        private void ListItem_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            var selected = ((FrameworkElement)sender).DataContext as SelectCategoryDescription;
            e.Handled = true;
            if (selected == null) return;
            NavigationCommands.GoToPage.Execute(GetPageAddress(selected), (IInputElement)sender);
        }

        public class ViewModel : NotifyPropertyChanged {
            public ViewModel() {
                FilesStorage.Instance.Watcher(ContentCategory.TrackCategories).Update += SelectTrackDialog_CategoriesViewModel_LibraryUpdate;
                Categories = new BetterObservableCollection<SelectCategoryDescription>(ReloadCategories());
            }

            private IEnumerable<SelectCategoryDescription> ReloadCategories() {
                return SelectCategoryDescription.LoadCategories(ContentCategory.TrackCategories);
            }

            private void SelectTrackDialog_CategoriesViewModel_LibraryUpdate(object sender, EventArgs e) {
                Categories.ReplaceEverythingBy(ReloadCategories());
            }

            public BetterObservableCollection<SelectCategoryDescription> Categories { get; }
        }
    }
}
