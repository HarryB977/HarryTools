using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Logger = HarryTools.LoggerTool.Logger;

namespace HarryTools.SceneRefs.Editor
{
    /// <summary>
    /// Gives every SceneId of one scene a new guid and an incremented id, as Ctrl+D does, and keeps the scene's own SceneRefs
    /// pointing to its own objects. Used to fix a duplicated scene or a merge that brought repeated guids.
    /// </summary>
    internal static class SceneIdRegenerator
    {
        // ==================== VARIABLES ====================

        private const string Category = "SceneRefs";

        // ==================== PUBLIC API ====================

        /// <summary>Opens the scene additively when it is closed, regenerates, saves it and closes it again.</summary>
        public static void RegenerateScene(string _scenePath)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Logger.LogWarning(Category, "Scene ids cannot be regenerated during Play.");
                return;
            }

            if (string.IsNullOrEmpty(_scenePath)) return;

            Scene _scene = SceneManager.GetSceneByPath(_scenePath);
            bool _wasLoaded = _scene.IsValid() && _scene.isLoaded;
            // A scene shown unloaded in the hierarchy is unloaded again afterwards instead of removed.
            bool _wasInHierarchy = _scene.IsValid();
            if (!_wasLoaded) _scene = EditorSceneManager.OpenScene(_scenePath, OpenSceneMode.Additive);
            if (!_scene.IsValid() || !_scene.isLoaded)
            {
                Logger.LogError(Category, $"Could not open scene '{_scenePath}' to regenerate its ids.");
                return;
            }

            try
            {
                Regenerate(_scene);
                EditorSceneManager.SaveScene(_scene);
            }
            finally
            {
                if (!_wasLoaded) EditorSceneManager.CloseScene(_scene, !_wasInHierarchy);
            }
        }

        // ==================== PRIVATE ====================

        private static void Regenerate(Scene _scene)
        {
            HashSet<string> _taken = SceneRefSync.TakenIds();
            Dictionary<string, SceneId> _byOldGuid = new Dictionary<string, SceneId>(StringComparer.Ordinal);

            foreach (GameObject _root in _scene.GetRootGameObjects())
            {
                foreach (SceneId _id in _root.GetComponentsInChildren<SceneId>(true))
                {
                    string _oldGuid = _id.Guid;
                    string _newId = SceneIdNaming.MakeUnique(SceneIdNaming.Increment(_id.Id), _taken.Contains);
                    SceneRefSync.WriteId(_id, SceneIdNaming.NewGuid(), _newId);
                    _taken.Add(_newId);
                    if (!string.IsNullOrEmpty(_oldGuid)) _byOldGuid[_oldGuid] = _id;
                }
            }

            RemapGuidOnlyRefs(_scene, _byOldGuid);
            SceneRefSync.SyncScene(_scene);
        }

        /// <summary>
        /// References with a live direct reference are fixed by the sync. References that only hold a guid of an object
        /// of this scene are moved to that object's new guid.
        /// </summary>
        private static void RemapGuidOnlyRefs(Scene _scene, Dictionary<string, SceneId> _byOldGuid)
        {
            foreach (MonoBehaviour _behaviour in SceneRefSync.Behaviours(_scene))
            {
                SerializedObject _so = new SerializedObject(_behaviour);
                bool _changed = false;
                foreach (SerializedProperty _ref in SceneRefPropertyWalker.FindAll(_so))
                {
                    if (_ref.FindPropertyRelative(SceneRefPropertyWalker.DirectField).objectReferenceValue != null) continue;

                    SerializedProperty _guid = _ref.FindPropertyRelative(SceneRefPropertyWalker.GuidField);
                    if (string.IsNullOrEmpty(_guid.stringValue) || !_byOldGuid.TryGetValue(_guid.stringValue, out SceneId _id)) continue;

                    _guid.stringValue = _id.Guid;
                    _ref.FindPropertyRelative(SceneRefPropertyWalker.LabelField).stringValue = $"{_id.Id} in {_scene.name}";
                    _changed = true;
                }

                if (_changed) _so.ApplyModifiedProperties();
            }
        }
    }
}
