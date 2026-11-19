using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Creates the empty table of a newly added locale in every dialogue collection and in the characters collection. Removing a locale leaves its tables untouched </summary>
    [InitializeOnLoad]
    internal static class DialogueLocaleWatcher {
        // ==================== VARIABLES ===================
        #region Private Variables
        private static bool suspended;
        #endregion

        // ==================== UNITY ====================
        #region Unity
        static DialogueLocaleWatcher() {
            LocalizationEditorSettings.EditorEvents.LocaleAdded -= OnLocaleAdded;
            LocalizationEditorSettings.EditorEvents.LocaleAdded += OnLocaleAdded;
        }
        #endregion

        // ==================== METHODS ====================
        #region Methods
        /// <summary> Adds the empty table of the locale to every given collection that lacks it </summary>
        internal static void AddLocaleTables(Locale _locale, IEnumerable<StringTableCollection> _collections) {
            if (_locale == null || _collections == null) return;
            foreach (StringTableCollection _collection in _collections) {
                if (_collection == null || _collection.GetTable(_locale.Identifier) != null) continue;
                _collection.AddNewTable(_locale.Identifier);
            }
        }

        /// <summary> The collections linked to the dialogues plus the characters collection, without repeats </summary>
        internal static List<StringTableCollection> Targets(IEnumerable<DialogueAsset> _dialogues, StringTableCollection _characters) {
            List<StringTableCollection> _targets = new List<StringTableCollection>();
            if (_dialogues != null) {
                foreach (DialogueAsset _asset in _dialogues) {
                    StringTableCollection _linked = _asset != null ? DialogueTables.Linked(_asset) : null;
                    if (_linked != null && !_targets.Contains(_linked)) _targets.Add(_linked);
                }
            }
            if (_characters != null && !_targets.Contains(_characters)) _targets.Add(_characters);
            return _targets;
        }

        /// <summary> The characters collection of the project. Null when it does not exist </summary>
        internal static StringTableCollection CharactersCollection() {
            return LocalizationEditorSettings.GetStringTableCollection(DialogueCharacterTables.CollectionName);
        }

        /// <summary> Adds the locale to the project without creating its tables in the project collections </summary>
        internal static void AddLocaleForTests(Locale _locale) {
            suspended = true;
            try {
                LocalizationEditorSettings.AddLocale(_locale);
            } finally {
                suspended = false;
            }
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        private static void OnLocaleAdded(Locale _locale) {
            if (suspended) return;
            AddLocaleTables(_locale, Targets(DialogueTables.ProjectDialogues(), CharactersCollection()));
        }
        #endregion
    }
}
