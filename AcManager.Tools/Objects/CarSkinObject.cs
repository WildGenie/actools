﻿using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Data;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Lists;
using AcManager.Tools.Managers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Objects {
    public class CarSkinObject : AcJsonObjectNew {
        public string CarId { get; }

        public CarSkinObject(string carId, IFileAcManager manager, string id, bool enabled)
                : base(manager, id, enabled) {
            CarId = carId;
        }

        /// <summary>
        /// So app will get names like “Black” instead of “09 Black” from “09_black”
        /// </summary>
        private static readonly Regex IdFix = new Regex(@"^\d\d?_", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        [NotNull]
        public static string NameFromId([NotNull] string id) {
            return AcStringValues.NameFromId(IdFix.Replace(id, ""));
        }

        protected override bool LoadJsonOrThrow() {
            if (!File.Exists(JsonFilename)) {
                ClearData();
                Name = NameFromId(Id);
                Changed = true;
                return true;
            }

            return base.LoadJsonOrThrow();
        }

        public override void Save() {
            if (HasData || string.IsNullOrEmpty(Name)) {
                base.Save();
            } else {
                var json = new JObject();
                SaveData(json);

                Changed = false;
                File.WriteAllText(JsonFilename, json.ToString());
            }
        }

        protected override void ClearData() {
            base.ClearData();
            DriverName = null;
            Team = null;
            SkinNumber = null;
            Priority = null;
        }

        public override void Reload() {
            base.Reload();
            OnImageChangedValue(PreviewImage);
            OnImageChangedValue(LiveryImage);
        }

        public override bool HandleChangedFile(string filename) {
            if (base.HandleChangedFile(filename)) {
                return true;
            }

            if (FileUtils.IsAffected(filename, PreviewImage)) {
                OnImageChangedValue(PreviewImage);
                CheckPreview();
                return true;
            }

            if (FileUtils.IsAffected(filename, LiveryImage)) {
                OnImageChangedValue(LiveryImage);
                CheckLivery();
                return true;
            }
            
            return true;
        }

        public override string DisplayName => string.IsNullOrWhiteSpace(Name) ? Id : Name;

        protected override void InitializeLocations() {
            base.InitializeLocations();
            JsonFilename = Path.Combine(Location, "ui_skin.json");
            LiveryImage = Path.Combine(Location, "livery.png");
            PreviewImage = Path.Combine(Location, "preview.jpg");
        }

        public string LiveryImage { get; private set; }

        public string PreviewImage { get; private set; }

        protected override void LoadOrThrow() {
            base.LoadOrThrow();
            CheckLivery();
            CheckPreview();
        }

        private void CheckLivery() {
            ErrorIf(!File.Exists(LiveryImage), AcErrorType.CarSkin_LiveryIsMissing, Id);
        }

        private void CheckPreview() {
            ErrorIf(!File.Exists(PreviewImage), AcErrorType.CarSkin_PreviewIsMissing, Id);
        }

        private string _driverName;

        [CanBeNull]
        public string DriverName {
            get { return _driverName; }
            set {
                if (Equals(value, _driverName)) return;
                _driverName = value;

                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                    SuggestionLists.RebuildCarSkinDriverNamesList();
                }
            }
        }

        private string _team;

        [CanBeNull]
        public string Team {
            get { return _team; }
            set {
                if (Equals(value, _team)) return;
                _team = value;

                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                    SuggestionLists.RebuildCarSkinTeamsList();
                }
            }
        }

        private string _skinNumber;

        [CanBeNull]
        public string SkinNumber {
            get { return _skinNumber; }
            set {
                if (Equals(value, _skinNumber)) return;
                _skinNumber = value;

                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private int? _priority;
        
        public int? Priority {
            get { return _priority; }
            set {
                if (Equals(value, _priority)) return;
                _priority = value;

                if (Loaded) {
                    OnPropertyChanged();
                    Changed = true;
                }
            }
        }

        private void LoadSkinRelated(JObject json) {
            DriverName = json.GetStringValueOnly("drivername")?.Trim();
            Team = json.GetStringValueOnly("team")?.Trim();
            SkinNumber = json.GetStringValueOnly("number")?.Trim();
            Priority = json.GetIntValueOnly("priority");
        }

        private void SaveSkinRelated(JObject json) {
            json[@"drivername"] = DriverName ?? string.Empty;
            json[@"team"] = Team ?? string.Empty;
            json[@"number"] = SkinNumber ?? string.Empty;

            if (Priority.HasValue && !SettingsHolder.Content.SkinsSkipPriority) {
                json[@"priority"] = Priority.Value;
            } else {
                json.Remove(@"priority");
            }
        }

        protected override void SaveCountry(JObject json) {
            json[@"country"] = Country ?? string.Empty;
        }

        public override void PastLoad() {
            // base.PastLoad();
            /* we don't need to add country and author to suggestion lists: one
               might be very invalid and other is missing here anyway */

            if (!Enabled) return;

            SuggestionLists.CarSkinTeamsList.AddUnique(Team);
            SuggestionLists.CarSkinDriverNamesList.AddUnique(DriverName);
        }

        protected override void LoadData(JObject json) {
            Name = json.GetStringValueOnly("skinname");
            LoadCountry(json);
            LoadSkinRelated(json);

            if (string.IsNullOrWhiteSpace(Name)) {
                // more than usual case
                // AddError(AcErrorType.Data_ObjectNameIsMissing);
            }

            // LoadTags(json);
            // LoadDescription(json);
            // LoadYear(json);
            // LoadVersionInfo(json);
        }

        public override void SaveData(JObject json) {
            json[@"skinname"] = Name ?? string.Empty;
            SaveCountry(json);
            SaveSkinRelated(json);

            json.Remove(@"tags");
            json.Remove(@"description");
            json.Remove(@"author");
            json.Remove(@"version");
            json.Remove(@"url");

            // SaveTags(json);
            // SaveDescription(json);
            // SaveYear(json);
            // SaveVersionInfo(json);
        }

        protected override KunosDlcInformation GetDlc() {
            return null;
        }

        protected override AutocompleteValuesList GetTagsList() {
            return SuggestionLists.CarSkinTagsList;
        }

        public ListCollectionView TeamsList => SuggestionLists.CarSkinDriverNamesList.View;
    }
}
