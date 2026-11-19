using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.Localization.Plugins.Google.Columns;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Pushes and pulls the tables of the dialogues and of the characters, each collection in a tab of its own. A push always pulls first, so sheet edits are never overwritten </summary>
    internal sealed class DialogueSheetSync {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string CharactersTabTitle = "Characters";
        private const string CharactersName = "Characters";
        private const string ProgressTitle = "Google Sheets";
        private const int TabIdLength = 6;

        private readonly IDialogueSheetGateway gateway;
        private readonly IDialogueSheetTabs tabs;
        #endregion

        // ==================== CONSTRUCTOR ====================
        #region Constructor
        /// <summary> A sync through the gateway that records the tabs in the project's sheet settings </summary>
        public DialogueSheetSync(IDialogueSheetGateway _gateway) : this(_gateway, DialogueSheetsSettings.instance) { }

        /// <summary> A sync through the gateway that records the tabs in the given store </summary>
        internal DialogueSheetSync(IDialogueSheetGateway _gateway, IDialogueSheetTabs _tabs) {
            gateway = _gateway ?? throw new ArgumentNullException(nameof(_gateway));
            tabs = _tabs ?? throw new ArgumentNullException(nameof(_tabs));
        }
        #endregion

        // ==================== METHODS ====================
        #region Methods
        /// <summary> The title of the tab of the dialogue: its name, then the start of its asset ID, since tab titles must be unique </summary>
        public static string TabTitle(DialogueAsset _asset) {
            if (_asset == null) return "";
            string _id = _asset.AssetId ?? "";
            return _asset.name + " (" + (_id.Length > TabIdLength ? _id.Substring(0, TabIdLength) : _id) + ")";
        }

        /// <summary> Takes the sheet edits of the dialogue's translations, then puts its table entries in script order. A dialogue without a collection has nothing to pull, so nothing is created for it </summary>
        public SyncResult Pull(DialogueAsset _asset) {
            if (_asset == null) return SyncResult.Failed("", "There is no dialogue to pull.");
            if (DialogueTables.Linked(_asset) == null) return SyncResult.Done(_asset.name, 0, 0);
            return Sync(_asset.name, () => DialogueTables.EnsureCollection(_asset), TabTitle(_asset), () => DialogueTableLayout.Refresh(_asset), false);
        }

        /// <summary> Pulls the dialogue and puts its entries in script order, then writes its table to the sheet. Nothing is written when the pull or the reorder fails, or when the access can only read </summary>
        public SyncResult Push(DialogueAsset _asset) {
            if (_asset == null) return SyncResult.Failed("", "There is no dialogue to push.");
            return Sync(_asset.name, () => DialogueTables.EnsureCollection(_asset), TabTitle(_asset), () => DialogueTableLayout.Refresh(_asset), true);
        }

        /// <summary> Takes the sheet edits of the character name translations </summary>
        public SyncResult PullCharacters() {
            return Sync(CharactersName, DialogueCharacterTables.EnsureCollection, CharactersTabTitle, DialogueCharacterTables.Refresh, false);
        }

        /// <summary> Pulls the character names and puts them in name order, then writes them to the sheet. Nothing is written when the pull or the reorder fails, or when the access can only read </summary>
        public SyncResult PushCharacters() {
            return Sync(CharactersName, DialogueCharacterTables.EnsureCollection, CharactersTabTitle, DialogueCharacterTables.Refresh, true);
        }

        /// <summary> Pulls every dialogue of the project and the characters </summary>
        public SyncSummary PullAll() {
            return PullAll(DialogueTables.ProjectDialogues());
        }

        /// <summary> Pushes every dialogue of the project and the characters </summary>
        public SyncSummary PushAll() {
            return PushAll(DialogueTables.ProjectDialogues());
        }

        /// <summary> The message of the exception, from inside the wrappers of tasks and reflection calls, with a hint when the spreadsheet was not found </summary>
        internal static string Describe(Exception _exception) {
            while (_exception is AggregateException || _exception is TargetInvocationException) {
                Exception _inner = _exception is AggregateException _aggregate && _aggregate.InnerExceptions.Count > 0 ? _aggregate.InnerExceptions[0] : _exception.InnerException;
                if (_inner == null) break;
                _exception = _inner;
            }
            if (_exception == null) return "Unknown error.";
            string _message = _exception.Message;
            if (_exception is Google.GoogleApiException _google && _google.HttpStatusCode == HttpStatusCode.NotFound) _message += " Check the spreadsheet ID.";
            return _message;
        }

        /// <summary> Pulls the given dialogues and the characters, going on when one fails </summary>
        internal SyncSummary PullAll(IReadOnlyList<DialogueAsset> _dialogues) {
            return RunAll(_dialogues, false);
        }

        /// <summary> Pushes the given dialogues and the characters, going on when one fails </summary>
        internal SyncSummary PushAll(IReadOnlyList<DialogueAsset> _dialogues) {
            return RunAll(_dialogues, true);
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        private SyncSummary RunAll(IReadOnlyList<DialogueAsset> _dialogues, bool _push) {
            SyncSummary _summary = new SyncSummary(_push);
            int _count = _dialogues != null ? _dialogues.Count : 0;
            bool _characters = DialogueCharacterTables.Find() != null;
            float _total = _count + (_characters ? 1 : 0);
            string _verb = _push ? "Pushing " : "Pulling ";

            try {
                for (int _i = 0; _i < _count; _i++) {
                    DialogueAsset _asset = _dialogues[_i];
                    if (_asset == null) continue;
                    EditorUtility.DisplayProgressBar(ProgressTitle, _verb + _asset.name, _i / _total);
                    _summary.Add(_push ? Push(_asset) : Pull(_asset));
                }
                // Without the collection there are no names to sync
                if (_characters) {
                    EditorUtility.DisplayProgressBar(ProgressTitle, _verb + CharactersName.ToLowerInvariant(), _count / _total);
                    _summary.Add(_push ? PushCharacters() : PullCharacters());
                }
            } finally {
                EditorUtility.ClearProgressBar();
            }
            return _summary;
        }

        private SyncResult Sync(string _name, Func<StringTableCollection> _getCollection, string _title, Func<bool> _refresh, bool _push) {
            if (_push && gateway.IsReadOnly) return SyncResult.Failed(_name, SheetConnection.ReadOnlyMessage);

            // Set once the pull worked, and right before the push is sent: a failure before that pushed nothing
            bool _pulled = false;
            bool _pushing = false;
            try {
                StringTableCollection _collection = _getCollection();
                if (_collection == null) return SyncResult.Failed(_name, "There is no table to sync: the project has no locales.");

                int _sheetId = ResolveTab(_collection, _title);
                SheetPullCapture _capture = null;
                int _conflicts = 0;
                // Without a tab, a read-only access has nothing to pull
                if (_sheetId >= 0) {
                    string _error = PullInto(_sheetId, _collection, out _capture, out _conflicts);
                    if (_error != null) return SyncResult.Failed(_name, _push ? NothingPushed(_error, false) : _error);
                }
                _pulled = true;
                int _unknownRows = _capture != null ? _capture.UnknownRows : 0;
                _refresh();

                if (_push) {
                    List<SheetColumn> _columns = DialogueSheetColumns.ForPush(_collection, _capture);
                    // The rows the pull could not match go back below the others, so nothing typed in them is lost
                    SheetGrid _tail = _capture != null ? _capture.Tail(_collection, _columns) : null;
                    _pushing = true;
                    gateway.Push(_sheetId, _collection, _columns, _tail);
                    // Only now did the texts reach the sheet
                    DialogueSheetColumns.CommitPush(_columns);
                }
                DialogueTables.SaveTables(_collection);

                SyncResult _result = SyncResult.Done(_name, _unknownRows, _conflicts);
                if (_unknownRows > 0 || _conflicts > 0) Debug.LogWarning((_push ? "Pushed " : "Pulled ") + _name + ". " + _result.Counts());
                return _result;
            } catch (Exception _exception) {
                string _message = Describe(_exception);
                return SyncResult.Failed(_name, _push && !_pushing ? NothingPushed(_message, _pulled) : _message);
            }
        }

        private static string NothingPushed(string _reason, bool _pulled) {
            return (_pulled ? "The pull worked but the push could not be prepared, so nothing was pushed. " : "The pull failed, so nothing was pushed. ") + _reason;
        }

        /// <summary> The tab of the collection: the one recorded for this spreadsheet while it still has it, else a tab with the title, else a new one. The result is recorded. -1 when there is none and the access can only read </summary>
        private int ResolveTab(StringTableCollection _collection, string _title) {
            string _guid = _collection.SharedData.TableCollectionNameGuid.ToString("N");
            string _spreadsheet = gateway.SpreadsheetId;
            IReadOnlyList<(int Id, string Title)> _sheets = gateway.GetSheets();
            bool _recorded = tabs.TryGetTab(_spreadsheet, _guid, out int _sheetId);

            if (_recorded && Has(_sheets, _sheetId)) return _sheetId;

            int _found = -1;
            for (int _i = 0; _i < _sheets.Count; _i++) {
                if (_sheets[_i].Title == _title) {
                    _found = _sheets[_i].Id;
                    break;
                }
            }
            if (_found < 0 && gateway.IsReadOnly) return -1;
            _sheetId = _found >= 0 ? _found : gateway.AddSheet(_title);
            tabs.SetTab(_spreadsheet, _guid, _sheetId);
            tabs.Save();
            return _sheetId;
        }

        private static bool Has(IReadOnlyList<(int Id, string Title)> _sheets, int _sheetId) {
            for (int _i = 0; _i < _sheets.Count; _i++) {
                if (_sheets[_i].Id == _sheetId) return true;
            }
            return false;
        }

        /// <summary> Reads the tab and gives each row to the pull columns, every cell taken from the column where its header is. The capture keeps the row of each matched entry and the rows that matched none: a key the collection lacks, a repeated key after its first row, or a row with content in a managed column but no key. An empty tab has nothing to pull and no capture. Returns why the pull cannot run, or null </summary>
        private string PullInto(int _sheetId, StringTableCollection _collection, out SheetPullCapture _capture, out int _conflicts) {
            _capture = null;
            _conflicts = 0;

            SheetGrid _grid = gateway.Read(_sheetId);
            List<string> _headers = _grid.Headers();
            if (_headers.TrueForAll(DialogueSheetColumns.IsBlank)) {
                // A tab never pushed to is empty; rows under a blank first row cannot be matched to columns
                return _grid.HasCells(1) ? "The tab has rows but its first row has no headers, so its columns cannot be matched." : null;
            }

            List<SheetColumn> _columns = DialogueSheetColumns.ForPull(_collection, _headers, out string _error);
            if (_columns == null) return _error;
            DialogueKeyColumn _key = (DialogueKeyColumn)_columns[0];
            SheetPullCapture _read = new SheetPullCapture(_grid, _headers, _key.ColumnIndex);
            List<int> _managed = ManagedColumns(_collection, _read);

            for (int _i = 0; _i < _columns.Count; _i++) _columns[_i].PullBegin(_collection);
            try {
                HashSet<long> _pulled = new HashSet<long>();
                for (int _row = 1; _row < _grid.RowCount; _row++) {
                    string _keyValue = _grid.Value(_row, _key.ColumnIndex);
                    string _keyNote = _grid.Note(_row, _key.ColumnIndex);
                    if (DialogueSheetColumns.IsBlank(_keyValue) && string.IsNullOrEmpty(_keyNote)) {
                        if (HasContent(_grid, _row, _managed)) _read.AddUnknown(_row);
                        continue;
                    }

                    SharedTableData.SharedTableEntry _entry = _key.PullKey(_keyValue, _keyNote);
                    // A repeated key keeps the first row
                    if (_entry == null || !_pulled.Add(_entry.Id)) {
                        _read.AddUnknown(_row);
                        continue;
                    }
                    _read.AddKnown(_entry.Id, _row);

                    for (int _i = 1; _i < _columns.Count; _i++) {
                        int _column = _columns[_i].ColumnIndex;
                        _columns[_i].PullCellData(_entry, _grid.Value(_row, _column), _grid.Note(_row, _column));
                    }
                }
            } finally {
                for (int _i = 0; _i < _columns.Count; _i++) _columns[_i].PullEnd();
            }

            _capture = _read;
            for (int _i = 0; _i < _columns.Count; _i++) {
                if (_columns[_i] is DialogueTranslationColumn _translation) _conflicts += _translation.Conflicts;
            }
            LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(this, _collection);
            return null;
        }

        /// <summary> The positions in the tab that was read of the columns a push writes </summary>
        private static List<int> ManagedColumns(StringTableCollection _collection, SheetPullCapture _read) {
            List<int> _managed = new List<int>();
            List<SheetColumn> _columns = DialogueSheetColumns.ForPush(_collection);
            for (int _i = 0; _i < _columns.Count; _i++) {
                int _index = _read.ColumnOf(_collection, _columns[_i]);
                if (_index >= 0) _managed.Add(_index);
            }
            return _managed;
        }

        private static bool HasContent(SheetGrid _grid, int _row, List<int> _columns) {
            for (int _i = 0; _i < _columns.Count; _i++) {
                if (!DialogueSheetColumns.IsBlank(_grid.Value(_row, _columns[_i])) || !string.IsNullOrEmpty(_grid.Note(_row, _columns[_i]))) return true;
            }
            return false;
        }
        #endregion
    }

    /// <summary> The outcome of syncing one collection </summary>
    internal sealed class SyncResult {
        // ==================== VARIABLES ===================
        #region Public Variables
        /// <summary> The dialogue name, or the characters </summary>
        public string Name { get; }

        /// <summary> Why the sync failed. Null when it worked </summary>
        public string Error { get; }

        /// <summary> True when the sync worked </summary>
        public bool Succeeded => Error == null;

        /// <summary> Sheet rows that matched no entry, so they were not imported. A push keeps them below the others </summary>
        public int UnknownRows { get; }

        /// <summary> Translations changed both locally and in the sheet since their last sync, which took the sheet text </summary>
        public int Conflicts { get; }
        #endregion

        // ==================== CONSTRUCTOR ====================
        #region Constructor
        private SyncResult(string _name, string _error, int _unknownRows, int _conflicts) {
            Name = _name ?? "";
            Error = _error;
            UnknownRows = _unknownRows;
            Conflicts = _conflicts;
        }
        #endregion

        // ==================== METHODS ====================
        #region Methods
        internal static SyncResult Done(string _name, int _unknownRows, int _conflicts) {
            return new SyncResult(_name, null, _unknownRows, _conflicts);
        }

        internal static SyncResult Failed(string _name, string _message) {
            return new SyncResult(_name, string.IsNullOrEmpty(_message) ? "Unknown error." : _message, 0, 0);
        }

        /// <summary> The unknown rows and the conflicts, in words </summary>
        public string Counts() {
            return CountsText(UnknownRows, Conflicts);
        }

        /// <summary> Unknown rows and conflicts, in words </summary>
        internal static string CountsText(int _unknownRows, int _conflicts) {
            return _unknownRows + (_unknownRows == 1 ? " unknown row kept in the sheet and not imported, " : " unknown rows kept in the sheet and not imported, ")
                + _conflicts + (_conflicts == 1 ? " conflict where the sheet text replaced a local edit." : " conflicts where the sheet text replaced a local edit.");
        }
        #endregion
    }

    /// <summary> The outcome of syncing several collections </summary>
    internal sealed class SyncSummary {
        // ==================== VARIABLES ===================
        #region Private Variables
        private readonly List<SyncResult> results = new List<SyncResult>();
        #endregion

        #region Public Variables
        /// <summary> True for a push, false for a pull </summary>
        public bool IsPush { get; }

        /// <summary> Why nothing was synced at all. Null when the collections were tried </summary>
        public string Error { get; }

        /// <summary> The result of each collection, in the order they were synced </summary>
        public IReadOnlyList<SyncResult> Results => results;

        /// <summary> Collections that failed </summary>
        public int Failures {
            get {
                int _failures = 0;
                for (int _i = 0; _i < results.Count; _i++) {
                    if (!results[_i].Succeeded) _failures++;
                }
                return _failures;
            }
        }

        /// <summary> Unknown rows over every collection </summary>
        public int UnknownRows {
            get {
                int _total = 0;
                for (int _i = 0; _i < results.Count; _i++) _total += results[_i].UnknownRows;
                return _total;
            }
        }

        /// <summary> Conflicts over every collection </summary>
        public int Conflicts {
            get {
                int _total = 0;
                for (int _i = 0; _i < results.Count; _i++) _total += results[_i].Conflicts;
                return _total;
            }
        }
        #endregion

        // ==================== CONSTRUCTOR ====================
        #region Constructor
        internal SyncSummary(bool _isPush, string _error = null) {
            IsPush = _isPush;
            Error = _error;
        }
        #endregion

        // ==================== METHODS ====================
        #region Methods
        internal void Add(SyncResult _result) {
            if (_result != null) results.Add(_result);
        }

        /// <summary> How many collections were synced, the counts, then each failure with its reason </summary>
        public string Describe() {
            if (Error != null) return Error;

            StringBuilder _text = new StringBuilder();
            _text.Append(IsPush ? "Pushed " : "Pulled ").Append(results.Count - Failures).Append(" of ").Append(results.Count).Append(results.Count == 1 ? " table." : " tables.");
            _text.Append("\n").Append(SyncResult.CountsText(UnknownRows, Conflicts));

            for (int _i = 0; _i < results.Count; _i++) {
                SyncResult _result = results[_i];
                if (_result.Succeeded && _result.UnknownRows == 0 && _result.Conflicts == 0) continue;
                _text.Append("\n- ").Append(_result.Name).Append(": ").Append(_result.Succeeded ? _result.Counts() : "failed. " + _result.Error);
            }
            return _text.ToString();
        }
        #endregion
    }
}
