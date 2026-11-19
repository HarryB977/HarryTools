using System.Collections.Generic;
using UnityEditor.Localization;
using UnityEditor.Localization.Plugins.Google.Columns;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace HarryTools.Dialogue.Editor {
    /// <summary> The sheet columns of a dialogue collection: key, key comment, source text, then a translation and a status column per other locale, in the order of the collection tables. Pushing and pulling use the same letters </summary>
    internal static class DialogueSheetColumns {
        // ==================== METHODS ====================
        #region Methods
        /// <summary> Every column, from A onwards </summary>
        internal static List<SheetColumn> ForPush(StringTableCollection _collection) {
            return Build(_collection, false);
        }

        /// <summary> The columns read back from the sheet: the key and the translation and status columns. The key comment and source columns are left out, so a pull never writes them </summary>
        internal static List<SheetColumn> ForPull(StringTableCollection _collection) {
            return Build(_collection, true);
        }

        /// <summary> True when the entry is one of the collection's own entries </summary>
        internal static bool Belongs(StringTableCollection _collection, SharedTableData.SharedTableEntry _keyEntry) {
            if (_collection == null || _collection.SharedData == null || _keyEntry == null) return false;
            return _collection.SharedData.GetEntry(_keyEntry.Id) == _keyEntry;
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        private static List<SheetColumn> Build(StringTableCollection _collection, bool _forPull) {
            List<SheetColumn> _columns = new List<SheetColumn>();
            if (_collection == null) return _columns;

            LocaleIdentifier? _source = DialogueTables.SourceLocale()?.Identifier;
            int _index = 0;

            Add(_columns, new DialogueKeyColumn(), ref _index, true);
            Add(_columns, new KeyCommentColumn(), ref _index, !_forPull);
            if (_source.HasValue) Add(_columns, new LocaleColumn { LocaleIdentifier = _source.Value }, ref _index, !_forPull);

            IReadOnlyList<StringTable> _tables = _collection.StringTables;
            for (int _i = 0; _i < _tables.Count; _i++) {
                if (_tables[_i] == null || (_source.HasValue && _tables[_i].LocaleIdentifier == _source.Value)) continue;
                LocaleIdentifier _locale = _tables[_i].LocaleIdentifier;
                Add(_columns, new DialogueTranslationColumn { LocaleIdentifier = _locale }, ref _index, true);
                Add(_columns, new DialogueStatusColumn { LocaleIdentifier = _locale }, ref _index, true);
            }
            return _columns;
        }

        /// <summary> Gives the column the next letter, and keeps it only when included, so left-out columns still hold their letter </summary>
        private static void Add(List<SheetColumn> _columns, SheetColumn _column, ref int _index, bool _include) {
            _column.ColumnIndex = _index++;
            if (_include) _columns.Add(_column);
        }
        #endregion
    }
}
