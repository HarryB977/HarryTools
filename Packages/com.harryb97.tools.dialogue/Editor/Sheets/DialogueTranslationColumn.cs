using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.Localization.Plugins.Google.Columns;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Sheet column of the translation into one locale that is not the source locale. Pulling writes a changed cell and records the source text it was written against; an empty cell never clears the local translation </summary>
    internal sealed class DialogueTranslationColumn : SheetColumn {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string PullUndoName = "Pull Translation";

        private int pushTableIndex = -1;
        private StringTableCollection pullCollection;
        private StringTable pullTable;
        #endregion

        #region Public Variables
        /// <summary> The locale of the translation </summary>
        public LocaleIdentifier LocaleIdentifier { get; set; }

        /// <inheritdoc/>
        public override PushFields PushFields => PushFields.Value;
        #endregion

        // ==================== PUSH ====================
        #region Push
        /// <inheritdoc/>
        public override void PushBegin(StringTableCollection _collection) {
            pushTableIndex = -1;
            if (_collection == null) return;
            IReadOnlyList<StringTable> _tables = _collection.StringTables;
            for (int _i = 0; _i < _tables.Count; _i++) {
                if (_tables[_i] != null && _tables[_i].LocaleIdentifier == LocaleIdentifier) {
                    pushTableIndex = _i;
                    return;
                }
            }
        }

        /// <inheritdoc/>
        public override void PushHeader(StringTableCollection _collection, out string _header, out string _headerNote) {
            _header = LocaleIdentifier.ToString();
            _headerNote = null;
        }

        /// <inheritdoc/>
        public override void PushCellData(SharedTableData.SharedTableEntry _keyEntry, IList<StringTableEntry> _tableEntries, out string _value, out string _note) {
            StringTableEntry _entry = pushTableIndex >= 0 && pushTableIndex < _tableEntries.Count ? _tableEntries[pushTableIndex] : null;
            _value = _entry?.Value;
            _note = null;
        }
        #endregion

        // ==================== PULL ====================
        #region Pull
        /// <summary> Finds the table of the locale. Nothing is pulled when the locale is the source locale </summary>
        public override void PullBegin(StringTableCollection _collection) {
            pullCollection = _collection;
            pullTable = null;
            if (_collection == null || DialogueTables.SourceLocale()?.Identifier == LocaleIdentifier) return;
            pullTable = _collection.GetTable(LocaleIdentifier) as StringTable;
        }

        /// <summary> Writes the cell when it has text that differs from the local translation, then stamps it, with undo. Empty cells and unknown entries are ignored </summary>
        public override void PullCellData(SharedTableData.SharedTableEntry _keyEntry, string _cellValue, string _cellNote) {
            if (pullTable == null || string.IsNullOrEmpty(_cellValue) || !DialogueSheetColumns.Belongs(pullCollection, _keyEntry)) return;

            StringTableEntry _entry = pullTable.GetEntry(_keyEntry.Id);
            if (_entry != null && _entry.Value == _cellValue) return;

            Undo.RecordObject(pullTable, PullUndoName);
            pullTable.AddEntry(_keyEntry.Id, _cellValue);
            EditorUtility.SetDirty(pullTable);
            DialogueTranslations.Stamp(pullCollection, _keyEntry.Key, LocaleIdentifier);
        }

        /// <inheritdoc/>
        public override void PullEnd() {
            pullCollection = null;
            pullTable = null;
        }
        #endregion
    }
}
