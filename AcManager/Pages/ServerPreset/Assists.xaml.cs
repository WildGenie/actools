﻿using System;
using System.Globalization;
using System.Windows.Data;
using AcManager.Pages.Drive;
using AcManager.Tools;
using FirstFloor.ModernUI.Windows.Converters;

namespace AcManager.Pages.ServerPreset {
    public partial class Assists {
        public Assists() {
            InitializeComponent();
        }

        public static IValueConverter SpecialOffForNegativeConverter { get; } = new SpecialOffForNegativeConverterInner();

        [ValueConversion(typeof(int), typeof(string))]
        private class SpecialOffForNegativeConverterInner : IValueConverter {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
                if (value == null) return null;
                var number = value.AsInt();
                return number < 0 ? ToolsStrings.AcSettings_Off : number.ToString();
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
                if (value == null) return null;
                return value as string == ToolsStrings.AcSettings_Off ? -1 : value.AsInt();
            }
        }
    }
}
