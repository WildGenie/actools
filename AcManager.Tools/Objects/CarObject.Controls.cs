﻿using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Managers.Presets;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public sealed partial class CarObject {
        private string _keySpecificControlsPreset, _keyControlsPresetFilename;

        private void InitializeControlsKeys() {
            if (_keySpecificControlsPreset != null) return;
            _keySpecificControlsPreset = $@"CarObject:{Id}:SpecificControlsPreset";
            _keyControlsPresetFilename = $@"CarObject:{Id}:ControlsPresetFilename";
        }

        private bool? _specificControlsPreset;

        public bool SpecificControlsPreset {
            get {
                InitializeControlsKeys();
                return _specificControlsPreset ?? (_specificControlsPreset = ValuesStorage.GetBool(_keySpecificControlsPreset)).Value;
            }
            set {
                if (Equals(value, SpecificControlsPreset)) return;
                InitializeControlsKeys();

                _specificControlsPreset = value;
                OnPropertyChanged();
                ValuesStorage.Set(_keySpecificControlsPreset, value);
            }
        }

        private string _currentControlsPresetName;

        [CanBeNull]
        public string CurrentControlsPresetName {
            get {
                if (!_controlsPresetFilenameLoaded) {
                    InitializeControlsKeys();
                    _controlsPresetFilenameLoaded = true;
                    _controlsPresetFilename = ValuesStorage.GetString(_keyControlsPresetFilename);
                    _currentControlsPresetName = _controlsPresetFilename == null ? null : ControlsSettings.GetCurrentPresetName(_controlsPresetFilename);
                }

                return _currentControlsPresetName;
            }
            private set {
                if (value == _currentControlsPresetName) return;
                _currentControlsPresetName = value;
                OnPropertyChanged();
            }
        }

        private string _controlsPresetFilename;
        private bool _controlsPresetFilenameLoaded;

        [CanBeNull]
        public string ControlsPresetFilename {
            get {
                if (!_controlsPresetFilenameLoaded) {
                    InitializeControlsKeys();
                    _controlsPresetFilenameLoaded = true;
                    _controlsPresetFilename = ValuesStorage.GetString(_keyControlsPresetFilename);
                }

                return _controlsPresetFilename;
            }
            set {
                if (Equals(value, ControlsPresetFilename)) return;
                InitializeControlsKeys();

                _controlsPresetFilename = value;
                CurrentControlsPresetName = value == null ? null : ControlsSettings.GetCurrentPresetName(value);
                OnPropertyChanged();
                ValuesStorage.Set(_keyControlsPresetFilename, value);
            }
        }

        public object SelectedControlsPreset {
            get { return null; }
            set {
                var entry = value as ISavedPresetEntry;
                if (entry != null) {
                    ControlsPresetFilename = entry.Filename;
                }
            }
        }
    }
}
