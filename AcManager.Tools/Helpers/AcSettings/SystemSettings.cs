﻿using System.Linq;
using AcTools.DataFile;
using AcTools.Utils;

namespace AcManager.Tools.Helpers.AcSettings {
    public class SystemSettings : IniSettings {
        internal SystemSettings() : base("assetto_corsa", systemConfig: true) {}

        public SettingEntry[] ScreenshotFormats { get; } = {
            new SettingEntry("JPG", ToolsStrings.AcSettings_ScreenshotFormat_Jpeg),
            new SettingEntry("BMP", ToolsStrings.AcSettings_ScreenshotFormat_Bmp)
        };

        #region Some controls stuff
        private bool _softLock;

        public bool SoftLock {
            get { return _softLock; }
            set {
                if (Equals(value, _softLock)) return;
                _softLock = value;
                OnPropertyChanged();
            }
        }

        /*private int _ffbSkipSteps;

        public int FfbSkipSteps {
            get { return _ffbSkipSteps; }
            set {
                value = value.Clamp(0, 1000);
                if (Equals(value, _ffbSkipSteps)) return;
                _ffbSkipSteps = value;
                OnPropertyChanged();
            }
        }*/
        #endregion

        #region Experimental FFB
        private bool _ffbGyro;

        public bool FfbGyro {
            get { return _ffbGyro; }
            set {
                if (Equals(value, _ffbGyro)) return;
                _ffbGyro = value;
                OnPropertyChanged();
            }
        }

        private int _ffbDamperMinLevel;

        public int FfbDamperMinLevel {
            get { return _ffbDamperMinLevel; }
            set {
                value = value.Clamp(0, 100);
                if (Equals(value, _ffbDamperMinLevel)) return;
                _ffbDamperMinLevel = value;
                OnPropertyChanged();
            }
        }

        private int _ffbDamperGain;

        public int FfbDamperGain {
            get { return _ffbDamperGain; }
            set {
                value = value.Clamp(0, 100);
                if (Equals(value, _ffbDamperGain)) return;
                _ffbDamperGain = value;
                OnPropertyChanged();
            }
        }
        #endregion

        #region Miscellaneous
        private int _simulationValue;

        public int SimulationValue {
            get { return _simulationValue; }
            set {
                value = value.Clamp(0, 100);
                if (Equals(value, _simulationValue)) return;
                _simulationValue = value;
                OnPropertyChanged();
            }
        }

        private bool _developerApps;

        public bool DeveloperApps {
            get { return _developerApps; }
            set {
                if (Equals(value, _developerApps)) return;
                _developerApps = value;
                OnPropertyChanged();
            }
        }

        private bool _hideDriver;

        public bool HideDriver {
            get { return _hideDriver; }
            set {
                if (Equals(value, _hideDriver)) return;
                _hideDriver = value;
                OnPropertyChanged();
            }
        }

        private bool _allowFreeCamera;

        public bool AllowFreeCamera {
            get { return _allowFreeCamera; }
            set {
                if (Equals(value, _allowFreeCamera)) return;
                _allowFreeCamera = value;
                OnPropertyChanged();
            }
        }

        private bool _logging;

        public bool Logging {
            get { return _logging; }
            set {
                if (Equals(value, _logging)) return;
                _logging = value;
                OnPropertyChanged();
            }
        }

        private SettingEntry _screenshotFormat;

        public SettingEntry ScreenshotFormat {
            get { return _screenshotFormat; }
            set {
                if (!ScreenshotFormats.Contains(value)) value = ScreenshotFormats[0];
                if (Equals(value, _screenshotFormat)) return;
                _screenshotFormat = value;
                OnPropertyChanged();
            }
        }

        public int MirrorsFieldOfViewDefault => 10;

        private int _mirrorsFieldOfView;

        public int MirrorsFieldOfView {
            get { return _mirrorsFieldOfView; }
            set {
                value = value.Clamp(1, 180);
                if (Equals(value, _mirrorsFieldOfView)) return;
                _mirrorsFieldOfView = value;
                OnPropertyChanged();
            }
        }

        public int MirrorsFarPlaneDefault => 400;

        private int _mirrorsFarPlane;

        public int MirrorsFarPlane {
            get { return _mirrorsFarPlane; }
            set {
                value = value.Clamp(10, 2000);
                if (Equals(value, _mirrorsFarPlane)) return;
                _mirrorsFarPlane = value;
                OnPropertyChanged();
            }
        }
        #endregion

        public void LoadFfbFromIni(IniFile ini) {
            SoftLock = ini["SOFT_LOCK"].GetBool("ENABLED", false);
            //FfbSkipSteps = ini["FORCE_FEEDBACK"].GetInt("FF_SKIP_STEPS", 1);

            var section = ini["FF_EXPERIMENTAL"];
            FfbGyro = section.GetBool("ENABLE_GYRO", false);
            FfbDamperMinLevel = section.GetDouble("DAMPER_MIN_LEVEL", 0d).ToIntPercentage();
            FfbDamperGain = section.GetDouble("DAMPER_GAIN", 1d).ToIntPercentage();
        }

        public void SaveFfbToIni(IniFile ini) {
            ini["SOFT_LOCK"].Set("ENABLED", SoftLock);
            //ini["FORCE_FEEDBACK"].Set("FF_SKIP_STEPS", FfbSkipSteps);

            var section = ini["FF_EXPERIMENTAL"];
            section.Set("ENABLE_GYRO", FfbGyro);
            section.Set("DAMPER_MIN_LEVEL", FfbDamperMinLevel.ToDoublePercentage());
            section.Set("DAMPER_GAIN", FfbDamperGain.ToDoublePercentage());
        }

        protected override void LoadFromIni() {
            LoadFfbFromIni(Ini);

            SimulationValue = Ini["ASSETTO_CORSA"].GetDouble("SIMULATION_VALUE", 0d).ToIntPercentage();
            DeveloperApps = Ini["AC_APPS"].GetBool("ENABLE_DEV_APPS", false);
            AllowFreeCamera = Ini["CAMERA"].GetBool("ALLOW_FREE_CAMERA", false);
            Logging = !Ini["LOG"].GetBool("SUPPRESS", false);
            HideDriver = Ini["DRIVER"].GetBool("HIDE", false);
            ScreenshotFormat = Ini["SCREENSHOT"].GetEntry("FORMAT", ScreenshotFormats);
            MirrorsFieldOfView = Ini["MIRRORS"].GetInt("FOV", MirrorsFieldOfViewDefault);
            MirrorsFarPlane = Ini["MIRRORS"].GetInt("FAR_PLANE", MirrorsFarPlaneDefault);
        }

        protected override void SetToIni() {
            SaveFfbToIni(Ini);

            Ini["ASSETTO_CORSA"].Set("SIMULATION_VALUE", SimulationValue.ToDoublePercentage());
            Ini["AC_APPS"].Set("ENABLE_DEV_APPS", DeveloperApps);
            Ini["CAMERA"].Set("ALLOW_FREE_CAMERA", AllowFreeCamera);
            Ini["LOG"].Set("SUPPRESS", !Logging);
            Ini["DRIVER"].Set("HIDE", HideDriver);
            Ini["SCREENSHOT"].Set("FORMAT", ScreenshotFormat);
            Ini["MIRRORS"].Set("FOV", MirrorsFieldOfView);
            Ini["MIRRORS"].Set("FAR_PLANE", MirrorsFarPlane);
        }
    }

    public class SystemOptionsSettings : IniSettings {
        internal SystemOptionsSettings() : base("options", systemConfig: true) { }

        private bool _ignoreResultTeleport;

        public bool IgnoreResultTeleport {
            get { return _ignoreResultTeleport; }
            set {
                if (Equals(value, _ignoreResultTeleport)) return;
                _ignoreResultTeleport = value;
                OnPropertyChanged();
            }
        }

        protected override void LoadFromIni() {
            var section = Ini["OPTIONS"];
            IgnoreResultTeleport = section.GetBool("IGNORE_RESULT_TELEPORT", false);
        }

        protected override void SetToIni() {
            var section = Ini["OPTIONS"];
            section.Set("IGNORE_RESULT_TELEPORT", IgnoreResultTeleport);
        }
    }
}
