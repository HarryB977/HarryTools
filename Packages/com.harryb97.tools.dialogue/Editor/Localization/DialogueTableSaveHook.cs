using UnityEditor;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Refreshes the context comments and the entry order of a dialogue's string table collection whenever the dialogue is saved </summary>
    internal sealed class DialogueTableSaveHook : AssetModificationProcessor {
        // ==================== PROCESSING ====================
        #region Processing
        public static string[] OnWillSaveAssets(string[] _paths) {
            for (int _i = 0; _i < _paths.Length; _i++) {
                if (!_paths[_i].EndsWith(".asset")) continue;
                DialogueAsset _asset = AssetDatabase.LoadAssetAtPath<DialogueAsset>(_paths[_i]);
                if (_asset != null) DialogueTableLayout.Refresh(_asset);
            }
            return _paths;
        }
        #endregion
    }
}
