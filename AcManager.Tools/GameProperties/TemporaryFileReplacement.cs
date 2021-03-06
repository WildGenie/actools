﻿using System;
using System.IO;
using AcManager.Tools.Managers;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.GameProperties {
    internal abstract class TemporaryDirectoryReplacementBase {
        private readonly string _relativeDestination;
        private readonly string _relativeBackup;

        internal TemporaryDirectoryReplacementBase([NotNull] string relativeDestination, string backupPostfix = @"_backup_cm") {
            _relativeDestination = relativeDestination;
            _relativeBackup = _relativeDestination + backupPostfix;
        }

        [NotNull]
        protected abstract string GetAbsolutePath([NotNull] string relative);

        protected bool Apply([NotNull] string source) {
            if (AcRootDirectory.Instance.Value == null || !Directory.Exists(source)) return false;

            var destination = GetAbsolutePath(_relativeDestination);
            var backup = GetAbsolutePath(_relativeBackup);

            if (Directory.Exists(destination)) {
                if (Directory.Exists(backup)) {
                    Directory.Move(backup, FileUtils.EnsureUnique(backup));
                }

                Logging.Debug($"{destination} → {backup}");
                Directory.Move(destination, backup);
            }

            try {
                Logging.Debug($"{source} → {destination}");
                FileUtils.HardlinkOrCopyRecursive(source, destination);
            } catch (Exception e) {
                // this exception should be catched here so original clouds folder still
                // will be restored even when copying a new one has been failed
                NonfatalError.Notify("Can’t replace directory", e);
            }

            return true;
        }

        public void Revert() {
            if (AcRootDirectory.Instance.Value == null) return;

            var destination = GetAbsolutePath(_relativeDestination);
            var backup = GetAbsolutePath(_relativeBackup);

            try {
                if (Directory.Exists(backup)) {
                    if (Directory.Exists(destination)) {
                        Directory.Delete(destination, true);
                    }

                    Directory.Move(backup, destination);
                }
            } catch (Exception e) {
                NonfatalError.Notify("Can’t restore original directory after replacing it with a temporary one", e);
            }
        }
    }

    internal abstract class TemporaryFileReplacementBase {
        private readonly string _relativeDestination;
        private readonly string _relativeBackup;

        internal TemporaryFileReplacementBase([NotNull] string relativeDestination, string backupPostfix = @"_backup_cm") {
            _relativeDestination = relativeDestination;
            _relativeBackup = _relativeDestination + backupPostfix;
        }

        [NotNull]
        protected abstract string GetAbsolutePath([NotNull] string relative);

        protected bool Apply([NotNull] string source) {
            if (AcRootDirectory.Instance.Value == null || !File.Exists(source)) return false;

            var destination = GetAbsolutePath(_relativeDestination);
            var backup = GetAbsolutePath(_relativeBackup);
            if (File.Exists(destination)) {
                if (File.Exists(backup)) {
                    File.Move(backup, FileUtils.EnsureUnique(backup));
                }

                Logging.Debug($"{destination} → {backup}");
                File.Move(destination, backup);
            }

            try {
                Logging.Debug($"{source} → {destination}");
                FileUtils.HardlinkOrCopy(source, destination);
            } catch (Exception e) {
                // this exception should be catched here so original clouds folder still
                // will be restored even when copying a new one has been failed
                NonfatalError.Notify("Can’t replace files", e);
            }

            return true;
        }

        public void Revert() {
            if (AcRootDirectory.Instance.Value == null) return;

            try {
                var destination = GetAbsolutePath(_relativeDestination);
                var backup = GetAbsolutePath(_relativeBackup);

                if (File.Exists(backup)) {
                    if (File.Exists(destination)) {
                        File.Delete(destination);
                    }

                    File.Move(backup, destination);
                }
            } catch (Exception e) {
                NonfatalError.Notify("Can’t restore original files after replacing them with temporary ones", e);
            }
        }
    }

    internal class TemporaryFileReplacement : TemporaryFileReplacementBase {
        private readonly string _source;

        public TemporaryFileReplacement([CanBeNull] string source, [NotNull] string relativeDestination, string backupPostfix = @"_backup_cm")
                : base(relativeDestination, backupPostfix) {
            _source = source;
        }

        public bool Apply() {
            return _source != null && Apply(_source);
        }

        protected override string GetAbsolutePath(string relative) {
            return relative;
        }
    }
}