﻿using System.Windows;

namespace FirstFloor.ModernUI.Windows.Attached {
    public static class InputBindingBehavior {
        public static bool GetPropagateInputBindingsToWindow(FrameworkElement obj) {
            return (bool)obj.GetValue(PropagateInputBindingsToWindowProperty);
        }

        public static void SetPropagateInputBindingsToWindow(FrameworkElement obj, bool value) {
            obj.SetValue(PropagateInputBindingsToWindowProperty, value);
        }

        public static readonly DependencyProperty PropagateInputBindingsToWindowProperty =
            DependencyProperty.RegisterAttached("PropagateInputBindingsToWindow", typeof(bool), typeof(InputBindingBehavior),
            new PropertyMetadata(false, OnPropagateInputBindingsToWindowChanged));

        private static void OnPropagateInputBindingsToWindowChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var element = (FrameworkElement)d;
            element.Loaded += FrameworkElement_Loaded;
            element.Unloaded += FrameworkElement_Unloaded;
        }

        public static void UpdateBindings(FrameworkElement obj) {
            if (obj.IsLoaded && GetPropagateInputBindingsToWindow(obj)) {
                RemoveBindings(obj);
                SetBindings(obj);
            }
        }

        public static Window GetWindow(DependencyObject obj) {
            return (Window)obj.GetValue(WindowProperty);
        }

        public static void SetWindow(DependencyObject obj, Window value) {
            obj.SetValue(WindowProperty, value);
        }

        public static readonly DependencyProperty WindowProperty = DependencyProperty.RegisterAttached("Window", typeof(Window),
                typeof(InputBindingBehavior), new UIPropertyMetadata(null));

        private static void SetBindings(UIElement element) {
            RemoveBindings(element);

            var window = Window.GetWindow(element);
            if (window == null) return;

            SetWindow(element, window);

            for (var i = element.InputBindings.Count - 1; i >= 0; i--) {
                var binding = element.InputBindings[i];
                binding.CommandTarget = element;
                window.InputBindings.Add(binding);
                element.InputBindings.RemoveAt(i);
            }
        }

        private static void RemoveBindings(UIElement element) {
            var window = GetWindow(element);
            if (window == null) return;

            element.ClearValue(WindowProperty);

            for (var i = window.InputBindings.Count - 1; i >= 0; i--) {
                var binding = window.InputBindings[i];
                if (ReferenceEquals(binding.CommandTarget, element)) {
                    window.InputBindings.RemoveAt(i);
                    element.InputBindings.Add(binding);
                }
            }
        }

        private static void FrameworkElement_Unloaded(object sender, RoutedEventArgs e) {
            /*var element = (FrameworkElement)sender;
            element.Unloaded -= FrameworkElement_Unloaded;
            RemoveBindings(element);*/

            RemoveBindings((FrameworkElement)sender);
        }

        private static void FrameworkElement_Loaded(object sender, RoutedEventArgs e) {
            /*var element = (FrameworkElement)sender;
            element.Loaded -= FrameworkElement_Loaded;
            SetBindings(element);*/

            SetBindings((FrameworkElement)sender);
        }
    }
}
