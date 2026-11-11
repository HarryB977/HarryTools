using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HarryTools.SceneRefs.Editor
{
    /// <summary>
    /// The operations behind the id row of the GameObject header, without any GUI so they can be tested. All of them support Undo.
    /// </summary>
    internal static class SceneIdHeaderActions
    {
        // ==================== PUBLIC API ====================

        /// <summary>Adds a SceneId to each object that has none, using its sanitized name as the base for a free id.</summary>
        public static void Add(GameObject[] _targets)
        {
            if (_targets == null || _targets.Length == 0) return;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Add Scene ID");
            HashSet<string> _taken = SceneRefSync.TakenIds();
            for (int _i = 0; _i < _targets.Length; _i++) SceneRefSync.EnsureId(_targets[_i], _taken);
        }

        /// <summary>Removes the SceneId component. References to the object stop resolving once its guid is gone.</summary>
        public static void Remove(GameObject _target)
        {
            if (_target == null || !_target.TryGetComponent(out SceneId _id)) return;
            Undo.DestroyObjectImmediate(_id);
        }

        /// <summary>Gives the object a new guid and keeps its visible id.</summary>
        public static void RegenerateGuid(GameObject _target)
        {
            if (_target == null || !_target.TryGetComponent(out SceneId _id)) return;

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("Regenerate Scene ID guid");
            SceneRefSync.WriteId(_id, SceneIdNaming.NewGuid(), _id.Id);
        }

        /// <summary>
        /// Saves the id even when another object already uses it. Returns false and "scene/path" of the other object in that case.
        /// Does nothing for prefab assets and Prefab Mode objects (returns true).
        /// </summary>
        public static bool TrySetId(GameObject _target, string _id, out string _conflictWith)
        {
            _conflictWith = null;
            if (string.IsNullOrWhiteSpace(_id)) return false;

            SceneId _sceneId = SceneRefSync.EnsureId(_target);
            if (_sceneId == null) return true;

            _conflictWith = FindConflict(_target, _id);
            if (_sceneId.Id != _id)
            {
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName("Edit Scene ID");
                SceneRefSync.WriteId(_sceneId, _sceneId.Guid, _id);
            }

            return _conflictWith == null;
        }

        /// <summary>
        /// "scene/path" of another object that uses the id, or null. Compares against the live SceneIds of the loaded scenes
        /// (excluding the target) and against the index entries of scenes that are not loaded.
        /// </summary>
        public static string FindConflict(GameObject _target, string _id)
        {
            if (string.IsNullOrEmpty(_id)) return null;

            SceneId _own = _target != null ? _target.GetComponent<SceneId>() : null;
            foreach (SceneId _other in SceneRefSync.LoadedSceneIds())
            {
                if (_other == _own || !SceneIdNaming.Comparer.Equals(_other.Id, _id)) continue;
                return $"{_other.gameObject.scene.name}/{SceneIndexer.HierarchyPath(_other.transform)}";
            }

            SceneIdIndex _index = SceneIdIndex.Existing;
            if (_index == null) return null;

            HashSet<string> _loaded = LoadedSceneKeys();
            foreach (SceneIdIndexEntry _entry in _index.Entries)
            {
                if (_loaded.Contains(_entry.SceneGuid) || !SceneIdNaming.Comparer.Equals(_entry.Id, _id)) continue;
                return $"{_entry.SceneName}/{_entry.HierarchyPath}";
            }

            return null;
        }

        // ==================== PRIVATE ====================

        private static HashSet<string> LoadedSceneKeys()
        {
            HashSet<string> _keys = new HashSet<string>();
            for (int _i = 0; _i < SceneManager.sceneCount; _i++)
            {
                Scene _scene = SceneManager.GetSceneAt(_i);
                if (_scene.isLoaded && !EditorSceneManager.IsPreviewScene(_scene)) _keys.Add(SceneIndexer.SceneKey(_scene));
            }

            return _keys;
        }
    }
}
