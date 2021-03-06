﻿using System;
using System.Windows.Input;
using AcManager.Tools.Managers;
using AcTools.DataFile;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Objects {
    public sealed partial class CarObject {
        private WeakReference<CarSetupsManager> _setupsManager;

        [CanBeNull]
        public CarSetupsManager GetSetupsManagerIfInitialized() {
            CarSetupsManager result;
            return _setupsManager != null && _setupsManager.TryGetTarget(out result) ? result : null;
        }

        [NotNull]
        public CarSetupsManager SetupsManager {
            get {
                CarSetupsManager result;
                if (_setupsManager != null && _setupsManager.TryGetTarget(out result)) {
                    return result;
                }

                result = CarSetupsManager.Create(this);
                _setupsManager = new WeakReference<CarSetupsManager>(result);
                return result;
            }
        }

        public void UpdateAcdData() {
            _acdData = null;
            OnPropertyChanged(nameof(AcdData));
            CommandManager.InvalidateRequerySuggested();
        }

        private bool _acdDataRead;
        private DataWrapper _acdData;

        /// <summary>
        /// Null only if data is damaged so much it’s unreadable.
        /// </summary>
        [CanBeNull]
        public DataWrapper AcdData {
            get {
                try {
                    if (_acdDataRead) return _acdData;
                    _acdDataRead = true;

                    _acdData = DataWrapper.FromDirectory(Location);
                    return _acdData;
                } catch (Exception e) {
                    NonfatalError.NotifyBackground("Can’t read data", e);
                    return null;
                }
            }
        }
    }
}
