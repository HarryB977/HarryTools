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
        private const string CharactersCollectionName = "DialogueCharacters";
        #endregion

        // ==================== UNITY ====================
        #region Unity
        static DialogueLocaleWatcher() {
            LocalizationEditorSettings.EditorEvents.LocaleAdded -= OnLocaleAdded;
            LocalizationEditorSettings.EditorEvents.LocaleAdded += OnLocaleAdded;
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        private static void OnLocaleAdded(Locale _locale) {
            foreach (StringTableCollection _collection in LocalizationEditorSettings.GetStringTableCollections()) {
                string _name = _collection.TableCollectionName;
                if (DialogueTables.IsDialogueCollectionName(_name) || _name == CharactersCollectionName) DialogueTables.EnsureLocaleTables(_collection);
            }
        }
        #endregion
    }
}
