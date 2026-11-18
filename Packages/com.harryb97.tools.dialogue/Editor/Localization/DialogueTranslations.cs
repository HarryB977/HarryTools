using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace HarryTools.Dialogue.Editor {
    /// <summary> How far each translation of a dialogue text is from its source text </summary>
    internal enum TranslationState {
        Untranslated,
        Outdated,
        UpToDate
    }

    /// <summary> Tracks which translations were written against an older source text, by keeping a <see cref="SourceSnapshot"/> on each translated entry </summary>
    internal static class DialogueTranslations {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string StampUndoName = "Stamp Translation";
        private const string ReviewUndoName = "Mark Translation as Reviewed";
        #endregion

        // ==================== METHODS ====================
        #region Methods
        /// <summary> Untranslated when the entry is missing or empty. Outdated when its snapshot differs from the current source text. Otherwise up to date, including a translation that has no snapshot yet </summary>
        internal static TranslationState State(StringTableCollection _collection, string _key, LocaleIdentifier _locale) {
            StringTableEntry _entry = EntryOf(_collection, _key, _locale, out _);
            if (_entry == null || string.IsNullOrEmpty(_entry.Value)) return TranslationState.Untranslated;

            SourceSnapshot _snapshot = _entry.GetMetadata<SourceSnapshot>();
            if (_snapshot == null) return TranslationState.UpToDate;
            return _snapshot.SourceText == SourceTextOf(_collection, _key) ? TranslationState.UpToDate : TranslationState.Outdated;
        }

        /// <summary> The source text the translation was last written or reviewed against. Null when it has no snapshot </summary>
        internal static string PreviousSource(StringTableCollection _collection, string _key, LocaleIdentifier _locale) {
            return EntryOf(_collection, _key, _locale, out _)?.GetMetadata<SourceSnapshot>()?.SourceText;
        }

        /// <summary> The text of the key in the table of the locale. Empty when there is none </summary>
        internal static string TextOf(StringTableCollection _collection, string _key, LocaleIdentifier _locale) {
            return EntryOf(_collection, _key, _locale, out _)?.Value ?? "";
        }

        /// <summary> Stores the current source text on the entry of the locale, with undo. Nothing when the entry does not exist </summary>
        internal static void Stamp(StringTableCollection _collection, string _key, LocaleIdentifier _locale) {
            Stamp(_collection, _key, _locale, StampUndoName);
        }

        /// <summary> Accepts the translation as matching the current source text, with undo </summary>
        internal static void MarkReviewed(StringTableCollection _collection, string _key, LocaleIdentifier _locale) {
            Stamp(_collection, _key, _locale, ReviewUndoName);
        }

        /// <summary> Accepts every translation of the key as matching the current source text, as one undo step </summary>
        internal static void MarkReviewed(StringTableCollection _collection, string _key) {
            if (_collection == null) return;
            LocaleIdentifier? _source = DialogueTables.SourceLocale()?.Identifier;
            IReadOnlyList<StringTable> _tables = _collection.StringTables;
            for (int _i = 0; _i < _tables.Count; _i++) {
                if (_tables[_i] == null || (_source.HasValue && _tables[_i].LocaleIdentifier == _source.Value)) continue;
                Stamp(_collection, _key, _tables[_i].LocaleIdentifier, ReviewUndoName);
            }
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        private static void Stamp(StringTableCollection _collection, string _key, LocaleIdentifier _locale, string _undoName) {
            StringTableEntry _entry = EntryOf(_collection, _key, _locale, out StringTable _table);
            if (_entry == null) return;

            string _source = SourceTextOf(_collection, _key);
            SourceSnapshot _snapshot = _entry.GetMetadata<SourceSnapshot>();
            if (_snapshot != null && _snapshot.SourceText == _source) return;

            Undo.RecordObject(_table, _undoName);
            if (_snapshot == null) _entry.AddMetadata(new SourceSnapshot { SourceText = _source });
            else _snapshot.SourceText = _source;
            EditorUtility.SetDirty(_table);
        }

        private static StringTableEntry EntryOf(StringTableCollection _collection, string _key, LocaleIdentifier _locale, out StringTable _table) {
            _table = null;
            if (_collection == null || string.IsNullOrEmpty(_key)) return null;
            SharedTableData.SharedTableEntry _shared = _collection.SharedData != null ? _collection.SharedData.GetEntry(_key) : null;
            if (_shared == null) return null;
            _table = _collection.GetTable(_locale) as StringTable;
            return _table != null ? _table.GetEntry(_shared.Id) : null;
        }

        /// <summary> The text of the key in the source locale table. Empty when there is none </summary>
        private static string SourceTextOf(StringTableCollection _collection, string _key) {
            Locale _source = DialogueTables.SourceLocale();
            if (_source == null) return "";
            return EntryOf(_collection, _key, _source.Identifier, out _)?.Value ?? "";
        }
        #endregion
    }
}
