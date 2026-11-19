using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Localization.Plugins.Google;
using UnityEngine;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Editor-only Google Sheets setup of the dialogue tables: the service provider, the spreadsheet and the tab of each collection. Stored in ProjectSettings, so it never reaches a build </summary>
    [FilePath("ProjectSettings/HarryToolsDialogueSheets.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class DialogueSheetsSettings : ScriptableSingleton<DialogueSheetsSettings> {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private SheetsServiceProvider provider;
        [SerializeField] private string spreadsheetId;
        [SerializeField] private List<Tab> tabs = new List<Tab>();
        #endregion

        #region Public Variables
        /// <summary> The asset that holds the Google credentials </summary>
        public SheetsServiceProvider Provider {
            get => provider;
            set => provider = value;
        }

        /// <summary> The ID of the spreadsheet, as it appears in its URL </summary>
        public string SpreadsheetId {
            get => spreadsheetId;
            set => spreadsheetId = value;
        }
        #endregion

        // ==================== METHODS ====================
        #region Methods
        /// <summary> The tab created for the collection. False when none was created yet </summary>
        public bool TryGetTab(string _collectionGuid, out int _sheetId) {
            Tab _tab = Find(_collectionGuid);
            _sheetId = _tab != null ? _tab.SheetId : 0;
            return _tab != null;
        }

        /// <summary> Records the tab created for the collection, replacing any previous one </summary>
        public void SetTab(string _collectionGuid, int _sheetId) {
            if (string.IsNullOrEmpty(_collectionGuid)) return;
            Tab _tab = Find(_collectionGuid);
            if (_tab == null) tabs.Add(new Tab { CollectionGuid = _collectionGuid, SheetId = _sheetId });
            else _tab.SheetId = _sheetId;
        }

        /// <summary> Writes the settings to their file in ProjectSettings </summary>
        public void Save() {
            Save(true);
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        private Tab Find(string _collectionGuid) {
            if (string.IsNullOrEmpty(_collectionGuid)) return null;
            for (int _i = 0; _i < tabs.Count; _i++) {
                if (tabs[_i] != null && tabs[_i].CollectionGuid == _collectionGuid) return tabs[_i];
            }
            return null;
        }
        #endregion

        // ==================== TYPES ====================
        #region Types
        /// <summary> A tab of the spreadsheet created for a collection. Its presence means the tab exists, since 0 is a valid tab ID </summary>
        [Serializable]
        private sealed class Tab {
            public string CollectionGuid;
            public int SheetId;
        }
        #endregion
    }
}
