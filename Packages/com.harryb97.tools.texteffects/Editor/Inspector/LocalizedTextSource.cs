#if HARRYTOOLS_TEXTEFFECTS_LOCALIZATION
using System.Collections.Generic;
using UnityEditor.Localization;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace HarryTools.TextEffects.Editor {
    /// <summary> Reads the texts of a LocalizedString straight from its string table collection, without changing the selected locale </summary>
    internal static class LocalizedTextSource {
        // ==================== METHODS ====================
        #region Methods
        /// <summary> Fills '_into' with the text of every locale that has a table, and returns the base locale. Null when the reference is not resolved </summary>
        internal static string ReadAll(LocalizedString _string, Dictionary<string, string> _into) {
            _into.Clear();
            if (!TryResolve(_string, out StringTableCollection _collection, out SharedTableData.SharedTableEntry _entry)) return null;

            IReadOnlyList<StringTable> _tables = _collection.StringTables;
            for (int _i = 0; _i < _tables.Count; _i++) {
                if (_tables[_i] == null) continue;
                _into[_tables[_i].LocaleIdentifier.Code] = _tables[_i].GetEntry(_entry.Id)?.Value;
            }
            return StringTableValidationMenu.FindBaseLocale(_tables);
        }

        /// <summary> The text of one locale, or null when the table or the entry is missing </summary>
        internal static string Read(LocalizedString _string, string _localeCode) {
            if (string.IsNullOrEmpty(_localeCode)) return null;
            if (!TryResolve(_string, out StringTableCollection _collection, out SharedTableData.SharedTableEntry _entry)) return null;

            StringTable _table = _collection.GetTable(new LocaleIdentifier(_localeCode)) as StringTable;
            return _table != null ? _table.GetEntry(_entry.Id)?.Value : null;
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        private static bool TryResolve(LocalizedString _string, out StringTableCollection _collection, out SharedTableData.SharedTableEntry _entry) {
            _collection = null;
            _entry = null;
            if (_string == null || _string.IsEmpty) return false;

            _collection = LocalizationEditorSettings.GetStringTableCollection(_string.TableReference);
            if (_collection == null || _collection.SharedData == null) return false;
            _entry = _collection.SharedData.GetEntryFromReference(_string.TableEntryReference);
            return _entry != null;
        }
        #endregion
    }
}
#endif
