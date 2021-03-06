using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using AcTools.AcdFile;
using AcTools.Utils;
using JetBrains.Annotations;

namespace AcTools.DataFile {
    public abstract class AbstractDataFile {
        public static ISyntaxErrorsCatcher ErrorsCatcher { get; set; }

        public enum StorageMode {
            UnpackedFile,
            AcdFile
        }

        [CanBeNull]
        private readonly Acd _acd;

        [NotNull]
        public readonly string Name;
        
        [CanBeNull]
        public readonly string Filename;

        public readonly StorageMode Mode;

        private readonly string _acdFilename;

        protected AbstractDataFile(string carDir, string name, Acd acd) {
            Name = name;
            _acdFilename = Path.Combine(carDir, "data.acd");

            if (acd != null) {
                _acd = acd;

                if (acd.IsPacked) {
                    Mode = StorageMode.AcdFile;
                    Filename = null;
                } else {
                    Mode = StorageMode.UnpackedFile;
                    Filename = acd.GetFilename(Name);
                }
            } else {
                if (File.Exists(_acdFilename)) {
                    Mode = StorageMode.AcdFile;
                    Filename = _acdFilename;
                } else {
                    Mode = StorageMode.UnpackedFile;
                    Filename = Path.Combine(carDir, "data", name);
                }
            }

            Load();
        }

        protected AbstractDataFile(string carDir, string filename) : this(carDir, filename, null) {}

        protected AbstractDataFile([CanBeNull] string filename) {
            Mode = StorageMode.UnpackedFile;
            if (filename == null) {
                Name = "";
                Filename = null;
            } else {
                Name = Path.GetFileName(filename);
                Filename = filename;
                Load();
            }
        }

        protected AbstractDataFile() : this(null) {}

        private void Load() {
            if (_acd != null || Mode == StorageMode.AcdFile) {
                // ReSharper disable once AssignNullToNotNullAttribute
                // with StorageMode.AcdFile, Filename is not null
                // TODO: sort everything better
                var acd = _acd ?? Acd.FromFile(Filename);

                var entry = acd.GetEntry(Name);
                if (entry != null) {
                    ParseString(entry.ToString());
                } else {
                    Clear();
                }
            } else if (Filename != null && File.Exists(Filename)) {
                ParseString(File.ReadAllText(Filename));
            } else {
                Clear();
            }
        }

        public Task SaveAsync(string filename = null, bool backup = false) {
            if (filename == null || Filename == filename) {
                if (Mode != StorageMode.UnpackedFile) {
                    UpdateAcd(backup);
                    return Task.Delay(0);
                }

                filename = Filename;
            }

            return SaveToAsync(filename, backup);
        }

        public void Save(string filename, bool backup = false) {
            if (filename == null || Filename == filename) {
                if (Mode != StorageMode.UnpackedFile) {
                    UpdateAcd(backup);
                    return;
                }

                filename = Filename;
            }

            SaveTo(filename, backup);
        }

        public void Save(bool backup = false) {
            Save(null, backup);
        }

        private void UpdateAcd(bool backup) {
            var acd = _acd;
            if (acd != null) {
                if (acd.IsPacked) {
                    acd.SetEntry(Name, Stringify());
                    acd.Save(_acdFilename);
                } else {
                    SaveTo(acd.GetFilename(Name), backup);
                }
            } else {
                var filename = Filename;
                if (filename == null) {
                    throw new Exception("File wasn�t loaded to be saved like this");
                }

                acd = Acd.FromFile(filename);
                acd.SetEntry(Name, Stringify());

                if (File.Exists(filename)) {
                    if (backup) {
                        FileUtils.Recycle(filename);
                    } else {
                        File.Delete(filename);
                    }
                }

                acd.Save(filename);
            }
        }

        protected virtual void SaveTo(string filename, bool backup) {
            if (File.Exists(filename) && backup) {
                FileUtils.Recycle(filename);
            }
            File.WriteAllText(filename, Stringify());
        }

        protected virtual Task SaveToAsync(string filename, bool backup) {
            if (File.Exists(filename) && backup) {
                FileUtils.Recycle(filename);
            }
            return FileUtils.WriteAllBytesAsync(filename, Encoding.UTF8.GetBytes(Stringify()));
        }

        protected abstract void ParseString(string file);

        public abstract void Clear();

        public abstract string Stringify();
    }
}