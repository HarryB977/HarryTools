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
            foreach (string _guid in AssetDatabase.FindAssets("t:DialogueAsset")) {
                DialogueAsset _asset = AssetDatabase.LoadAssetAtPath<DialogueAsset>(AssetDatabase.GUIDToAssetPath(_guid));
                if (_asset == null) continue;
                StringTableCollection _linked = DialogueTables.Linked(_asset);
                if (_linked != null) DialogueTables.EnsureLocaleTables(_linked);
            }
            StringTableCollection _characters = LocalizationEditorSettings.GetStringTableCollection(CharactersCollectionName);
            if (_characters != null) DialogueTables.EnsureLocaleTables(_characters);
        }
        #endregion
    }
}
