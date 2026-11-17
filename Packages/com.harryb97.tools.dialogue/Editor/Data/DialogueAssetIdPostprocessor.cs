using UnityEditor;
using UnityEngine;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Keeps <see cref="DialogueAsset.AssetId"/> equal to the AssetDatabase GUID: a duplicated asset gets a new one, a renamed or moved asset keeps its own </summary>
    internal class DialogueAssetIdPostprocessor : AssetPostprocessor {
        // ==================== PROCESSING ====================
        #region Processing
        private static void OnPostprocessAllAssets(string[] _imported, string[] _deleted, string[] _moved, string[] _movedFrom) {
            for (int _i = 0; _i < _imported.Length; _i++) {
                if (!_imported[_i].EndsWith(".asset")) continue;
                DialogueAsset _asset = AssetDatabase.LoadAssetAtPath<DialogueAsset>(_imported[_i]);
                if (_asset != null) Sync(_asset, _imported[_i]);
            }
        }

        /// <summary> Writes the GUID of the asset at the given path into its serialized id when they differ </summary>
        internal static bool Sync(DialogueAsset _asset, string _path) {
            string _guid = AssetDatabase.AssetPathToGUID(_path);
            if (string.IsNullOrEmpty(_guid) || _asset.SerializedAssetId == _guid) return false;

            _asset.SetAssetId(_guid);
            EditorUtility.SetDirty(_asset);
            AssetDatabase.SaveAssetIfDirty(_asset);
            return true;
        }
        #endregion
    }
}
