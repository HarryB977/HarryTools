using System.Collections.Generic;
using HarryTools.DomainReload;
using UnityEditor;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Keeps <see cref="DialogueAsset.AssetId"/> equal to the AssetDatabase GUID: a duplicated asset gets a new one, a renamed or moved asset keeps its own. A duplicate also gets its own ids and string table collection. Characters get the same stable id </summary>
    internal class DialogueAssetIdPostprocessor : AssetPostprocessor {
        // ==================== VARIABLES ===================
        #region Private Variables
        // GUIDs of duplicates whose texts still live in the original's collection
        [ClearOnReload] private static List<string> pendingTexts;
        // GUIDs of duplicated characters whose name still is the original's entry
        [ClearOnReload] private static List<string> pendingForks;
        #endregion

        // ==================== PROCESSING ====================
        #region Processing
        private static void OnPostprocessAllAssets(string[] _imported, string[] _deleted, string[] _moved, string[] _movedFrom) {
            for (int _i = 0; _i < _imported.Length; _i++) {
                if (!_imported[_i].EndsWith(".asset")) continue;
                System.Type _type = AssetDatabase.GetMainAssetTypeAtPath(_imported[_i]);
                if (_type == typeof(DialogueAsset)) {
                    DialogueAsset _asset = AssetDatabase.LoadAssetAtPath<DialogueAsset>(_imported[_i]);
                    if (_asset != null) Sync(_asset, _imported[_i]);
                } else if (_type == typeof(DialogueCharacter)) {
                    DialogueCharacter _character = AssetDatabase.LoadAssetAtPath<DialogueCharacter>(_imported[_i]);
                    if (_character != null) Sync(_character, _imported[_i]);
                }
            }
        }

        /// <summary> Writes the GUID of the asset at the given path into its serialized id when they differ. When the old id belongs to another existing dialogue, the asset is a duplicate and is made independent of it </summary>
        internal static bool Sync(DialogueAsset _asset, string _path) {
            string _guid = AssetDatabase.AssetPathToGUID(_path);
            string _previous = _asset.SerializedAssetId;
            if (string.IsNullOrEmpty(_guid) || _previous == _guid) return false;

            bool _duplicate = !string.IsNullOrEmpty(_previous) && AssetDatabase.LoadAssetAtPath<DialogueAsset>(AssetDatabase.GUIDToAssetPath(_previous)) != null;
            _asset.SetAssetId(_guid);
            if (_duplicate) {
                _asset.RegenerateIds();
                _asset.TableCollectionGuid = "";
            }
            EditorUtility.SetDirty(_asset);
            AssetDatabase.SaveAssetIfDirty(_asset);

            // Creating the collection is an asset operation, which waits until the import has finished
            if (_duplicate) ScheduleTexts(_guid);
            return true;
        }

        /// <summary> Makes the character id equal to the GUID of the asset at the given path. That covers a character that had no id yet and a duplicate, which arrives with the id of the original. A duplicate whose name is the original's entry in the characters collection gets its own copy of it once the import has finished </summary>
        internal static bool Sync(DialogueCharacter _character, string _path) {
            string _guid = AssetDatabase.AssetPathToGUID(_path);
            string _previous = _character.CharacterId;
            if (string.IsNullOrEmpty(_guid) || _previous == _guid) return false;

            _character.CharacterId = _guid;
            EditorUtility.SetDirty(_character);
            AssetDatabase.SaveAssetIfDirty(_character);

            if (!string.IsNullOrEmpty(_previous) && PointsToEntry(_character, _previous)) ScheduleFork(_guid);
            return true;
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        private static void ScheduleTexts(string _guid) {
            if (pendingTexts == null) pendingTexts = new List<string>();
            if (pendingTexts.Contains(_guid)) return;
            pendingTexts.Add(_guid);
            if (pendingTexts.Count == 1) EditorApplication.update += AdoptPendingTexts;
        }

        private static bool PointsToEntry(DialogueCharacter _character, string _key) {
            if (_character.DisplayName == null || _character.DisplayName.IsEmpty) return false;
            UnityEditor.Localization.StringTableCollection _collection = UnityEditor.Localization.LocalizationEditorSettings.GetStringTableCollection(_character.DisplayName.TableReference);
            return _collection != null && _collection.SharedData != null && _collection.SharedData.GetEntryFromReference(_character.DisplayName.TableEntryReference)?.Key == _key;
        }

        private static void ScheduleFork(string _guid) {
            if (pendingForks == null) pendingForks = new List<string>();
            if (pendingForks.Contains(_guid)) return;
            pendingForks.Add(_guid);
            if (pendingForks.Count == 1) EditorApplication.update += ForkPendingCharacters;
        }

        /// <summary> Gives every pending duplicated character its own copy of the name. Runs on its own once the Editor is idle </summary>
        internal static void ForkPendingCharacters() {
            if (EditorApplication.isUpdating || EditorApplication.isCompiling) return;
            EditorApplication.update -= ForkPendingCharacters;
            if (pendingForks == null) return;

            string[] _guids = pendingForks.ToArray();
            pendingForks.Clear();
            for (int _i = 0; _i < _guids.Length; _i++) {
                DialogueCharacter _character = AssetDatabase.LoadAssetAtPath<DialogueCharacter>(AssetDatabase.GUIDToAssetPath(_guids[_i]));
                // Only what was moved is written: the character and the characters collection
                if (_character == null || !DialogueCharacterTables.MoveToOwnCollection(_character)) continue;
                AssetDatabase.SaveAssetIfDirty(_character);
                DialogueTables.SaveTables(DialogueCharacterTables.Find());
            }
        }

        // An update callback rather than delayCall, which does not run while the Editor is in the background
        private static void AdoptPendingTexts() {
            if (EditorApplication.isUpdating || EditorApplication.isCompiling) return;
            EditorApplication.update -= AdoptPendingTexts;
            if (pendingTexts == null) return;

            string[] _guids = pendingTexts.ToArray();
            pendingTexts.Clear();
            for (int _i = 0; _i < _guids.Length; _i++) {
                DialogueAsset _asset = AssetDatabase.LoadAssetAtPath<DialogueAsset>(AssetDatabase.GUIDToAssetPath(_guids[_i]));
                if (_asset != null) DialogueTables.AdoptTexts(_asset);
            }
        }
        #endregion
    }
}
