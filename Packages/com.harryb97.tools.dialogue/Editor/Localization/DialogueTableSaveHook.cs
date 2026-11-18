using System.Collections.Generic;
using UnityEditor;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Refreshes the context comments and the entry order of a dialogue's string table collection whenever the dialogue is saved </summary>
    internal sealed class DialogueTableSaveHook : AssetModificationProcessor {
        // ==================== PROCESSING ====================
        #region Processing
        /// <summary> Refreshes the collection of every saved dialogue and adds the shared table data it changed to the paths being saved </summary>
        public static string[] OnWillSaveAssets(string[] _paths) {
            List<string> _extra = null;
            for (int _i = 0; _i < _paths.Length; _i++) {
                if (!_paths[_i].EndsWith(".asset") || AssetDatabase.GetMainAssetTypeAtPath(_paths[_i]) != typeof(DialogueAsset)) continue;
                DialogueAsset _asset = AssetDatabase.LoadAssetAtPath<DialogueAsset>(_paths[_i]);
                if (_asset == null || !DialogueTableLayout.Refresh(_asset)) continue;

                string _shared = AssetDatabase.GetAssetPath(DialogueTables.Linked(_asset).SharedData);
                if (string.IsNullOrEmpty(_shared) || System.Array.IndexOf(_paths, _shared) >= 0) continue;
                if (_extra == null) _extra = new List<string>();
                if (!_extra.Contains(_shared)) _extra.Add(_shared);
            }
            if (_extra == null) return _paths;

            string[] _all = new string[_paths.Length + _extra.Count];
            _paths.CopyTo(_all, 0);
            _extra.CopyTo(_all, _paths.Length);
            return _all;
        }
        #endregion
    }
}
