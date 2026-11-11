using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HarryTools.SceneRefs.Editor
{
    /// <summary>
    /// Gives a new guid and an incremented id to the SceneIds of a hierarchy created by Ctrl+D, paste or instantiation
    /// when their guid already belongs to another object, then syncs the SceneRefs of the new hierarchy so they follow the copies.
    /// </summary>
    [InitializeOnLoad]
    internal static class SceneIdDuplicateFixer
    {
        // ==================== VARIABLES ====================

        /// <summary>Times the existing guids were gathered; tests use it to check the early return.</summary>
        internal static int GatherCount;

        // ==================== SETUP ====================

        static SceneIdDuplicateFixer()
        {
            ObjectChangeEvents.changesPublished -= OnChangesPublished;
            ObjectChangeEvents.changesPublished += OnChangesPublished;
        }

        // ==================== PUBLIC API ====================

        /// <summary>Only the new hierarchy changes; the objects it was copied from keep their guid and id.</summary>
        public static void FixNewHierarchy(GameObject _root) => FixNewHierarchies(new[] { _root });

        /// <summary>Fixes several new roots of one change stream, gathering the existing guids once for all of them.</summary>
        public static void FixNewHierarchies(IReadOnlyList<GameObject> _roots)
        {
            List<GameObject> _fixRoots = new List<GameObject>();
            List<SceneId[]> _rootIds = new List<SceneId[]>();
            HashSet<SceneId> _inTrees = new HashSet<SceneId>();
            for (int _i = 0; _i < _roots.Count; _i++)
            {
                GameObject _root = _roots[_i];
                if (_root == null || !SceneRefSync.IsSceneObject(_root)) continue;

                SceneId[] _ids = _root.GetComponentsInChildren<SceneId>(true);
                if (_ids.Length == 0) continue;

                _fixRoots.Add(_root);
                _rootIds.Add(_ids);
                _inTrees.UnionWith(_ids);
            }

            if (_fixRoots.Count == 0) return;

            HashSet<string> _existing = ExistingGuids(_inTrees);
            HashSet<string> _taken = null;

            for (int _r = 0; _r < _fixRoots.Count; _r++)
            {
                SceneId[] _newIds = _rootIds[_r];
                for (int _i = 0; _i < _newIds.Length; _i++)
                {
                    SceneId _id = _newIds[_i];
                    // Adding the guid also catches two objects of the new trees that share one.
                    if (!string.IsNullOrEmpty(_id.Guid) && _existing.Add(_id.Guid)) continue;

                    _taken ??= SceneRefSync.TakenIds();
                    string _newId = SceneIdNaming.MakeUnique(SceneIdNaming.Increment(_id.Id), _taken.Contains);
                    SceneRefSync.WriteId(_id, SceneIdNaming.NewGuid(), _newId);
                    _taken.Add(_newId);
                    _existing.Add(_id.Guid);
                }

                List<MonoBehaviour> _behaviours = new List<MonoBehaviour>();
                SceneRefSync.AddBehaviours(_fixRoots[_r], _behaviours);
                for (int _i = 0; _i < _behaviours.Count; _i++)
                {
                    SerializedObject _so = new SerializedObject(_behaviours[_i]);
                    foreach (SerializedProperty _ref in SceneRefPropertyWalker.FindAll(_so)) SceneRefSync.SyncProperty(_ref, _taken);
                }
            }
        }

        // ==================== PRIVATE ====================

        private static void OnChangesPublished(ref ObjectChangeEventStream _stream)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            List<GameObject> _roots = null;
            for (int _i = 0; _i < _stream.length; _i++)
            {
                if (_stream.GetEventType(_i) != ObjectChangeKind.CreateGameObjectHierarchy) continue;

                _stream.GetCreateGameObjectHierarchyEvent(_i, out CreateGameObjectHierarchyEventArgs _args);
                // The event only exposes an instance id; EntityIdToObject does not exist in 6000.0, the minimum version.
#pragma warning disable CS0618
                if (EditorUtility.InstanceIDToObject(_args.instanceId) is GameObject _root) (_roots ??= new List<GameObject>()).Add(_root);
#pragma warning restore CS0618
            }

            if (_roots != null) FixNewHierarchies(_roots);
        }

        /// <summary>Guids of every other SceneId: live ones of the loaded scenes and the index entries of closed scenes.</summary>
        private static HashSet<string> ExistingGuids(HashSet<SceneId> _exclude)
        {
            GatherCount++;
            HashSet<string> _guids = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> _loadedScenes = new HashSet<string>(StringComparer.Ordinal);
            for (int _i = 0; _i < SceneManager.sceneCount; _i++)
            {
                Scene _scene = SceneManager.GetSceneAt(_i);
                if (_scene.isLoaded) _loadedScenes.Add(SceneIndexer.SceneKey(_scene));
            }

            foreach (SceneId _id in SceneRefSync.LoadedSceneIds())
                if (!_exclude.Contains(_id) && !string.IsNullOrEmpty(_id.Guid)) _guids.Add(_id.Guid);

            SceneIdIndex _index = SceneIdIndex.Existing;
            if (_index != null)
                foreach (SceneIdIndexEntry _entry in _index.Entries)
                    if (!_loadedScenes.Contains(_entry.SceneGuid) && !string.IsNullOrEmpty(_entry.Guid)) _guids.Add(_entry.Guid);
            return _guids;
        }
    }
}
