﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Tools.Managers;
using AcTools.DataFile;
using AcTools.Utils;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.AcSettings {
    public abstract class IniSettings : NotifyPropertyChanged {
        private static readonly Dictionary<string, FileSystemWatcher> Watchers = new Dictionary<string, FileSystemWatcher>();

        private static FileSystemWatcher GetWatcher(string directory) {
            FileSystemWatcher result;
            if (Watchers.TryGetValue(directory, out result)) return result;

            Directory.CreateDirectory(directory);
            result = new FileSystemWatcher {
                Path = directory,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                Filter = "*",
                EnableRaisingEvents = true,
                IncludeSubdirectories = true
            };

            Watchers[directory] = result;
            return result;
        }

        public string Filename { get; }

        protected IniSettings([Localizable(false)] string name, bool reload = true, bool systemConfig = false) {
            try {
                var directory = systemConfig ? FileUtils.GetSystemCfgDirectory(AcRootDirectory.Instance.RequireValue) :
                        FileUtils.GetDocumentsCfgDirectory();

                Filename = Path.Combine(directory, name + ".ini");
                if (reload) {
                    Reload();
                }

                var watcher = GetWatcher(directory);
                watcher.Changed += OnChanged;
                watcher.Created += OnChanged;
                watcher.Deleted += OnChanged;
                watcher.Renamed += OnRenamed;
            } catch (Exception e) {
                Logging.Warning("IniSettings exception: " + e);
            }
        }

        protected virtual void OnRenamed(object sender, RenamedEventArgs e) {
            if (FileUtils.IsAffected(e.OldFullPath, Filename) || FileUtils.IsAffected(e.FullPath, Filename)) {
                ReloadLater();
            }
        }

        protected virtual void OnChanged(object sender, FileSystemEventArgs e) {
            if (FileUtils.IsAffected(e.FullPath, Filename)) {
                ReloadLater();
            }
        }

        protected void Replace(IniFile ini, bool backup = false) {
            IsSaving = false;
            IgnoreChangesForAWhile();
            ini.Save(Filename, backup);
            Ini = ini;
            IsLoading = true;
            LoadFromIni();
            IsLoading = false;
        }

        protected void Reload() {
            IsReloading = false;
            IsSaving = false;
            Ini = new IniFile(Filename);
            IsLoading = true;
            LoadFromIni();
            IsLoading = false;
        }

        private DateTime _lastSaved;

        protected bool IsLoading { get; private set; }

        protected bool IsReloading { get; private set; }

        protected bool IsSaving { get; set; }

        private void IgnoreChangesForAWhile() {
            _lastSaved = DateTime.Now;
        }

        private async void ReloadLater() {
            if (IsReloading || IsSaving || DateTime.Now - _lastSaved < TimeSpan.FromSeconds(3)) return;

            IsReloading = true;
            await Task.Delay(200);
            if (!IsReloading) return;

            try {
                int i;
                for (i = 0; i < 5; i++) {
                    try {
                        Ini = new IniFile(Filename);
                        break;
                    } catch (Exception) {
                        await Task.Delay(100);
                    }
                }

                if (i == 5) {
                    Logging.Warning("Can’t load config file: " + Path.GetFileName(Filename));
                    return;
                }

                IsLoading = true;
                ActionExtension.InvokeInMainThread(LoadFromIni);
                IsLoading = false;
            } finally {
                IsReloading = false;
            }
        }

        protected virtual async void Save() {
            if (IsSaving || IsLoading || Ini == null) return;

            IsSaving = true;
            await Task.Delay(500);
            if (!IsSaving) return;

            try {
                SetToIni();
                IgnoreChangesForAWhile();
                IsReloading = false;

                await Ini.SaveAsync(Filename);
            } catch (Exception e) {
                NonfatalError.Notify(ToolsStrings.AcSettings_CannotSave, ToolsStrings.AcSettings_CannotSave_Commentary, e);
            } finally {
                IsSaving = false;
            }
        }

        public void SaveImmediately() {
            try {
                SetToIni();
                IgnoreChangesForAWhile();
                Ini.Save(Filename);
            } catch (Exception e) {
                NonfatalError.Notify(ToolsStrings.AcSettings_CannotSave, ToolsStrings.AcSettings_CannotSave_Commentary, e);
            } finally {
                IsSaving = false;
            }

            IsSaving = false;
        }

        protected void ForceSave() {
            var l = IsLoading;
            IsLoading = false;
            Save();
            IsLoading = l;
        }

        protected IniFile Ini { get; private set; }

        /// <summary>
        /// Called from IniSettings constructor!
        /// </summary>
        protected abstract void LoadFromIni();

        protected abstract void SetToIni();

        [NotifyPropertyChangedInvocator]
        protected override void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            OnPropertyChanged(true, propertyName);
        }

        [NotifyPropertyChangedInvocator]
        protected void OnPropertyChanged(bool save = true, [CallerMemberName] string propertyName = null) {
            base.OnPropertyChanged(propertyName);
            if (save) {
                Save();
            }
        }
    }
}