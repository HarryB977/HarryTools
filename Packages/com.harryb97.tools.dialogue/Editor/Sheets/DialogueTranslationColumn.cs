using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.Localization.Plugins.Google.Columns;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Sheet column of the translation into one locale that is not the source locale. Each translation remembers the text of its last sync, so a pull only takes a cell the sheet changed since then; when both sides changed, the sheet wins and the case is counted. An empty cell never clears the local translation </summary>
    internal sealed class DialogueTranslationColumn : SheetColumn {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string PullUndoName = "Pull Translation";

        private StringTable pushTable;
        private int noteColumn = -1;
        private readonly List<(long Id, string Text)> pushed = new List<(long, string)>();
        private StringTableCollection pullCollection;
        private StringTable pullTable;
        #endregion

        #region Public Variables
        /// <summary> The locale of the translation </summary>
        public LocaleIdentifier LocaleIdentifier { get; set; }

        /// <summary> Translations of the last pull that had changed both locally and in the sheet since their last sync, and took the sheet text. A translation cleared locally counts too. Reset when a pull begins </summary>
        public int Conflicts { get; private set; }

        /// <summary> What the pull before the push read. Its cell notes are pushed back on the row of their key. Null pushes no notes </summary>
        public SheetPullCapture Capture { get; set; }

        /// <summary> The value holds the translation; the note holds the cell note the pull before the push read for the key, so it moves with its row </summary>
        public override PushFields PushFields => PushFields.ValueAndNote;
        #endregion

        // ==================== PUSH ====================
        #region Push
        /// <inheritdoc/>
        public override void PushBegin(StringTableCollection _collection) {
            pushTable = null;
            pushed.Clear();
            noteColumn = Capture != null ? Capture.ColumnOf(_collection, this) : -1;
            if (_collection == null) return;
            IReadOnlyList<StringTable> _tables = _collection.StringTables;
            for (int _i = 0; _i < _tables.Count; _i++) {
                if (_tables[_i] != null && _tables[_i].LocaleIdentifier == LocaleIdentifier) {
                    pushTable = _tables[_i];
                    return;
                }
            }
        }

        /// <inheritdoc/>
        public override void PushHeader(StringTableCollection _collection, out string _header, out string _headerNote) {
            _header = LocaleIdentifier.ToString();
            _headerNote = null;
        }

        /// <summary> The local translation, and the cell note read for the key. The translation is only recorded as synced by <see cref="CommitPush"/>, once the push succeeded </summary>
        public override void PushCellData(SharedTableData.SharedTableEntry _keyEntry, IList<StringTableEntry> _tableEntries, out string _value, out string _note) {
            StringTableEntry _entry = pushTable != null && _keyEntry != null ? pushTable.GetEntry(_keyEntry.Id) : null;
            _value = _entry?.Value;
            _note = _keyEntry != null ? Capture?.Note(_keyEntry.Id, noteColumn) : null;
            if (_entry != null) pushed.Add((_keyEntry.Id, _value));
        }

        /// <summary> Records the pushed texts as the last synced ones. This is sync bookkeeping, so it is not undoable. Call it only after the push reached the sheet </summary>
        internal void CommitPush() {
            if (pushTable == null || pushed.Count == 0) return;
            for (int _i = 0; _i < pushed.Count; _i++) {
                StringTableEntry _entry = pushTable.GetEntry(pushed[_i].Id);
                if (_entry != null) SetSynced(_entry, pushed[_i].Text);
            }
            EditorUtility.SetDirty(pushTable);
            pushed.Clear();
        }
        #endregion

        // ==================== PULL ====================
        #region Pull
        /// <summary> Finds the table of the locale. Nothing is pulled when the locale is the source locale </summary>
        public override void PullBegin(StringTableCollection _collection) {
            pullCollection = _collection;
            pullTable = null;
            Conflicts = 0;
            if (_collection == null || DialogueTables.SourceLocale()?.Identifier == LocaleIdentifier) return;
            pullTable = _collection.GetTable(LocaleIdentifier) as StringTable;
        }

        /// <summary> Takes the cell when the sheet changed it since the last sync, then stamps it, with undo. A cell equal to the local text or to the last synced text keeps the local one. Blank cells and unknown entries are ignored </summary>
        public override void PullCellData(SharedTableData.SharedTableEntry _keyEntry, string _cellValue, string _cellNote) {
            if (pullTable == null || DialogueSheetColumns.IsBlank(_cellValue) || !DialogueSheetColumns.Belongs(pullCollection, _keyEntry)) return;

            StringTableEntry _entry = pullTable.GetEntry(_keyEntry.Id);
            string _cell = DialogueSheetColumns.Normalize(_cellValue);
            string _local = _entry != null ? DialogueSheetColumns.Normalize(_entry.Value) : "";
            string _synced = _entry?.GetMetadata<SheetSyncSnapshot>()?.SyncedText;
            if (_synced != null) _synced = DialogueSheetColumns.Normalize(_synced);

            if (_cell == _local) {
                // Sync bookkeeping only, so it is not undoable
                if (_synced != _cell) {
                    SetSynced(_entry, _cellValue);
                    EditorUtility.SetDirty(pullTable);
                }
                return;
            }
            // The sheet kept the synced text: any difference is a local edit
            if (_cell == _synced) return;

            // A local edit since the last sync is lost to the sheet text, including a translation cleared locally
            if (_synced != null ? _local != _synced : _local.Length > 0) Conflicts++;

            Undo.RecordObject(pullTable, PullUndoName);
            _entry = pullTable.AddEntry(_keyEntry.Id, _cellValue);
            SetSynced(_entry, _cellValue);
            EditorUtility.SetDirty(pullTable);
            DialogueTranslations.Stamp(pullCollection, _keyEntry.Key, LocaleIdentifier);
        }

        /// <inheritdoc/>
        public override void PullEnd() {
            pullCollection = null;
            pullTable = null;
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        private static void SetSynced(StringTableEntry _entry, string _text) {
            SheetSyncSnapshot _snapshot = _entry.GetMetadata<SheetSyncSnapshot>();
            if (_snapshot == null) _entry.AddMetadata(new SheetSyncSnapshot { SyncedText = _text ?? "" });
            else _snapshot.SyncedText = _text ?? "";
        }
        #endregion
    }
}
