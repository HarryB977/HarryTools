using UnityEditor;

namespace HarryTools.Audio.Editor {
    /// <summary> Gives every catalog item a stable id equal to its asset GUID, and fixes duplicated assets. </summary>
    internal static class AudioIdAssigner {
        // ==================== PUBLIC ===================
        /// <summary> Returns true when the id changed (the item is saved). </summary>
        public static bool EnsureId(AudioCatalogItem _item) {
            if (_item == null) return false;
            string _path = AssetDatabase.GetAssetPath(_item);
            if (string.IsNullOrEmpty(_path)) return false;
            string _guid = AssetDatabase.AssetPathToGUID(_path);
            string _id = _item.Id;

            if (_id == _guid) return false;
            if (!string.IsNullOrEmpty(_id)) {
                // A copy carries the original id, whose GUID still resolves to another asset.
                string _otherPath = AssetDatabase.GUIDToAssetPath(_id);
                if (string.IsNullOrEmpty(_otherPath) || _otherPath == _path) return false;
            }

            _item.SetId(_guid);
            EditorUtility.SetDirty(_item);
            AssetDatabase.SaveAssetIfDirty(_item);
            return true;
        }
    }
}
