using System;
using System.Collections.Generic;
using UnityEditor.Localization;
using UnityEditor.Localization.Plugins.Google.Columns;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace HarryTools.Dialogue.Editor {
    /// <summary> The sheet columns of a dialogue collection. Pushing writes key, key comment, source text, then a translation and a status column per other locale, in the order of the collection tables. Pulling finds its columns by their header titles </summary>
    internal static class DialogueSheetColumns {
        // ==================== METHODS ====================
        #region Methods
        /// <summary> Every column, from A onwards. Given what the pull before the push read, the key and translation cells keep their notes on the row of their key </summary>
        internal static List<SheetColumn> ForPush(StringTableCollection _collection, SheetPullCapture _capture = null) {
            List<SheetColumn> _columns = new List<SheetColumn>();
            if (_collection == null) return _columns;

            LocaleIdentifier? _source = DialogueTables.SourceLocale()?.Identifier;
            _columns.Add(new DialogueKeyColumn { Capture = _capture });
            _columns.Add(new KeyCommentColumn());
            if (_source.HasValue) _columns.Add(new LocaleColumn { LocaleIdentifier = _source.Value });
            foreach (LocaleIdentifier _locale in TranslationLocales(_collection, _source)) {
                _columns.Add(new DialogueTranslationColumn { LocaleIdentifier = _locale, Capture = _capture });
                _columns.Add(new DialogueStatusColumn { LocaleIdentifier = _locale });
            }
            for (int _i = 0; _i < _columns.Count; _i++) _columns[_i].ColumnIndex = _i;
            return _columns;
        }

        /// <summary> The columns read back from the sheet, placed where the header row has their titles: the key, then the translation and status columns of each locale that is not the source locale. A translation header is found by the locale code in its trailing parentheses, so the language name before it may differ. The key comment and source columns are left out, so a pull never writes them. Locales without their header are skipped. Null, with the reason, when the sheet has no key header </summary>
        internal static List<SheetColumn> ForPull(StringTableCollection _collection, IList<string> _headers, out string _error) {
            _error = null;
            if (_collection == null) {
                _error = "There is no collection to pull into.";
                return null;
            }

            List<SheetColumn> _columns = new List<SheetColumn>();
            DialogueKeyColumn _key = new DialogueKeyColumn();
            if (!Place(_collection, _key, _headers)) {
                _error = "The sheet has no \"" + KeyColumn.ColumnHeader + "\" header.";
                return null;
            }
            _columns.Add(_key);

            foreach (LocaleIdentifier _locale in TranslationLocales(_collection, DialogueTables.SourceLocale()?.Identifier)) {
                DialogueTranslationColumn _translation = new DialogueTranslationColumn { LocaleIdentifier = _locale };
                if (Place(_collection, _translation, _headers)) _columns.Add(_translation);
                DialogueStatusColumn _status = new DialogueStatusColumn { LocaleIdentifier = _locale };
                if (Place(_collection, _status, _headers)) _columns.Add(_status);
            }
            return _columns;
        }

        /// <summary> Records the texts of the last push as synced on every translation column. Call it only after the push reached the sheet </summary>
        internal static void CommitPush(IList<SheetColumn> _columns) {
            if (_columns == null) return;
            for (int _i = 0; _i < _columns.Count; _i++) {
                if (_columns[_i] is DialogueTranslationColumn _translation) _translation.CommitPush();
            }
        }

        /// <summary> True when the entry is one of the collection's own entries </summary>
        internal static bool Belongs(StringTableCollection _collection, SharedTableData.SharedTableEntry _keyEntry) {
            if (_collection == null || _collection.SharedData == null || _keyEntry == null) return false;
            return _collection.SharedData.GetEntry(_keyEntry.Id) == _keyEntry;
        }

        /// <summary> The position in the header row of the title the column pushes. Locale columns match by the locale code in the trailing parentheses of the title, ignoring case; the others match the whole title, ignoring case and surrounding spaces. -1 when the row lacks it </summary>
        internal static int HeaderIndex(StringTableCollection _collection, SheetColumn _column, IList<string> _headers) {
            if (_column == null || _headers == null) return -1;
            _column.PushHeader(_collection, out string _title, out _);
            string _code = _column is DialogueTranslationColumn _translation ? _translation.LocaleIdentifier.Code
                : _column is LocaleColumn _locale ? _locale.LocaleIdentifier.Code : null;

            for (int _i = 0; _i < _headers.Count; _i++) {
                string _header = _headers[_i]?.Trim();
                if (string.IsNullOrEmpty(_header)) continue;
                bool _match = _code != null ? string.Equals(TrailingCode(_header), _code, StringComparison.OrdinalIgnoreCase) : string.Equals(_header, _title, StringComparison.OrdinalIgnoreCase);
                if (_match) return _i;
            }
            return -1;
        }

        /// <summary> True when a cell holds no text, or only whitespace </summary>
        internal static bool IsBlank(string _cell) {
            return string.IsNullOrWhiteSpace(_cell);
        }

        /// <summary> The text with "\n" line endings, as the sheet returns it. Empty for null </summary>
        internal static string Normalize(string _text) {
            return string.IsNullOrEmpty(_text) ? "" : _text.Replace("\r\n", "\n").Replace('\r', '\n');
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        private static IEnumerable<LocaleIdentifier> TranslationLocales(StringTableCollection _collection, LocaleIdentifier? _source) {
            IReadOnlyList<StringTable> _tables = _collection.StringTables;
            for (int _i = 0; _i < _tables.Count; _i++) {
                if (_tables[_i] == null || (_source.HasValue && _tables[_i].LocaleIdentifier == _source.Value)) continue;
                yield return _tables[_i].LocaleIdentifier;
            }
        }

        /// <summary> Gives the column the position of its header in the header row. False when the row lacks it </summary>
        private static bool Place(StringTableCollection _collection, SheetColumn _column, IList<string> _headers) {
            int _index = HeaderIndex(_collection, _column, _headers);
            if (_index < 0) return false;
            _column.ColumnIndex = _index;
            return true;
        }

        /// <summary> The text inside the parentheses that end the header. Null when it does not end with them </summary>
        private static string TrailingCode(string _header) {
            if (!_header.EndsWith(")")) return null;
            int _open = _header.LastIndexOf('(');
            return _open >= 0 ? _header.Substring(_open + 1, _header.Length - _open - 2).Trim() : null;
        }
        #endregion
    }
}
