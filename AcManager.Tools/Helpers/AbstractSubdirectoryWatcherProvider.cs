﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using FirstFloor.ModernUI;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers {
    public abstract class AbstractSubdirectoryWatcherProvider : IDisposable {
        public class ContentWatcher : IDisposable {
            private readonly string _path;

            public ContentWatcher(string path) {
                _path = path;
            }

            private FileSystemWatcher _fsWatcher;
            private event EventHandler UpdateInternal;

            public event EventHandler Update {
                add {
                    if (UpdateInternal == null){
                        SetFsWatcher();
                    }
                    UpdateInternal += value;
                }
                remove {
                    UpdateInternal -= value;
                    if (UpdateInternal == null){
                        UnsetFsWatcher();
                    }
                }
            }

            private void SetFsWatcher() {
                if (_fsWatcher != null) UnsetFsWatcher();

                _fsWatcher = new FileSystemWatcher {
                    Path = _path,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                    Filter = "*.*"
                };
                _fsWatcher.Changed += FsWatcher_Changed;
                _fsWatcher.Created += FsWatcher_Changed;
                _fsWatcher.Deleted += FsWatcher_Changed;
                _fsWatcher.Renamed += FsWatcher_Changed;
                _fsWatcher.EnableRaisingEvents = true;
            }

            private void UnsetFsWatcher() {
                if (_fsWatcher == null) return;
                _fsWatcher.EnableRaisingEvents = false;
                _fsWatcher.Changed -= FsWatcher_Changed;
                _fsWatcher.Created -= FsWatcher_Changed;
                _fsWatcher.Deleted -= FsWatcher_Changed;
                _fsWatcher.Renamed -= FsWatcher_Changed;
                _fsWatcher.Dispose();
                _fsWatcher = null;
            }

            private void FsWatcher_Changed(object sender, FileSystemEventArgs e) {
                Dispatch();
            }

            private bool _dispatched;

            private async void Dispatch() {
                if (_dispatched) return;
                _dispatched = true;

                await Task.Delay(300);
                ActionExtension.InvokeInMainThreadAsync(() => {
                    UpdateInternal?.Invoke(this, new EventArgs());
                    _dispatched = false;
                });
            }

            public void Dispose() {
                UnsetFsWatcher();
            }
        }

        public void Dispose() {
            foreach (var value in _watchers.Values) {
                value.Dispose();
            }
            _watchers.Clear();
        }

        private Dictionary<string, ContentWatcher> _watchers;

        public virtual ContentWatcher Watcher(params string[] name) {
            if (_watchers == null) {
                _watchers = new Dictionary<string, ContentWatcher>();
            }

            string key;
            string local;

            if (name.Length == 0) {
                key = "";
                local = null;
            } else {
                key = Path.Combine(name);
                local = key;
            }

            ContentWatcher result;
            if (_watchers.TryGetValue(key, out result)) return result;

            var directory = GetSubdirectoryFilename(local);
            if (!Directory.Exists(directory)) {
                Directory.CreateDirectory(directory);
            }

            _watchers[key] = new ContentWatcher(directory);
            return _watchers[key];
        }

        protected abstract string GetSubdirectoryFilename([CanBeNull] string name);
    }
}
