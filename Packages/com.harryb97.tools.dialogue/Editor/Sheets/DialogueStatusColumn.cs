using System.Collections.Generic;
using UnityEditor.Localization;
using UnityEditor.Localization.Plugins.Google.Columns;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Tool-owned sheet column that marks the outdated translations of one locale. Clearing the mark in the sheet accepts the translation on the next pull. It never adds entries </summary>
    internal sealed class DialogueStatusColumn : SheetColumn {
        // ==================== VARIABLES ===================
        #region Private Variables
        /// <summary> First line of the cell of an outdated translation </summary>
        internal const string OutdatedMark = "Outdated";
        /// <summary> Suffix of the column header, after the locale code </summary>
        internal const string HeaderSuffix = " Status";

        private StringTableCollection collection;
        #endregion

        #region Public Variables
        /// <summary> The locale of the translation </summary>
        public LocaleIdentifier LocaleIdentifier { get; set; }

        /// <summary> The value holds the mark; the note holds the source text the mark was pushed against </summary>
        public override PushFields PushFields => PushFields.ValueAndNote;
        #endregion

        // ==================== PUSH ====================
        #region Push
        /// <inheritdoc/>
        public override void PushBegin(StringTableCollection _collection) {
            collection = _collection;
        }

        /// <inheritdoc/>
        public override void PushHeader(StringTableCollection _collection, out string _header, out string _headerNote) {
            _header = LocaleIdentifier.Code + HeaderSuffix;
            _headerNote = null;
        }

        /// <summary> For an outdated translation, the mark and the previous source text as the value, and the current source text as the note. Empty otherwise </summary>
        public override void PushCellData(SharedTableData.SharedTableEntry _keyEntry, IList<StringTableEntry> _tableEntries, out string _value, out string _note) {
            _value = null;
            _note = null;
            if (_keyEntry == null || DialogueTranslations.State(collection, _keyEntry.Key, LocaleIdentifier) != TranslationState.Outdated) return;

            _value = OutdatedMark + "\n" + DialogueTranslations.PreviousSourcePrefix + DialogueTranslations.PreviousSource(collection, _keyEntry.Key, LocaleIdentifier);
            _note = DialogueTranslations.SourceTextOf(collection, _keyEntry.Key);
        }

        /// <inheritdoc/>
        public override void PushEnd() {
            collection = null;
        }
        #endregion

        // ==================== PULL ====================
        #region Pull
        /// <inheritdoc/>
        public override void PullBegin(StringTableCollection _collection) {
            collection = _collection;
        }

        /// <summary> Marks the translation as reviewed when its mark was cleared in the sheet: the cell is empty, the translation is outdated, and the note shows the mark was pushed against the current source text. A mark that was never pushed cannot have been cleared, so a translation that became outdated after the last push keeps its state </summary>
        public override void PullCellData(SharedTableData.SharedTableEntry _keyEntry, string _cellValue, string _cellNote) {
            if (!string.IsNullOrEmpty(_cellValue) || _cellNote == null || !DialogueSheetColumns.Belongs(collection, _keyEntry)) return;
            if (DialogueTranslations.State(collection, _keyEntry.Key, LocaleIdentifier) != TranslationState.Outdated) return;
            if (_cellNote != DialogueTranslations.SourceTextOf(collection, _keyEntry.Key)) return;

            DialogueTranslations.MarkReviewed(collection, _keyEntry.Key, LocaleIdentifier);
        }

        /// <inheritdoc/>
        public override void PullEnd() {
            collection = null;
        }
        #endregion
    }
}
