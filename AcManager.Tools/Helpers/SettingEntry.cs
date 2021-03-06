﻿using System.ComponentModel;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {
    public sealed class SettingEntry : Displayable, IWithId, IWithId<int?> {
        public SettingEntry([LocalizationRequired(false)] string value, string displayName) {
            DisplayName = displayName;
            Value = value;
            IntValue = value.ToInvariantInt();
        }

        public SettingEntry(int value, string displayName) {
            DisplayName = displayName;
            Value = value.ToInvariantString();
            IntValue = value;
        }

        [Localizable(false)]
        public string Value { get; }

        public int? IntValue { get; }

        [Localizable(false)]
        public string Id => Value;

        int? IWithId<int?>.Id => IntValue;
    }
}