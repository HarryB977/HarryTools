using System;
using System.Collections.Generic;
using HarryTools.DomainReload;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Logger = HarryTools.LoggerTool.Logger;
using Object = UnityEngine.Object;

namespace HarryTools.SceneRefs.Editor
{
    /// <summary>
    /// Keeps prefab assets free of SceneIds. When a prefab under Assets is imported with SceneIds of its own, they are removed
    /// from the asset and the scene instances that had them get them back as added components with the same guid and id.
    /// SceneIds that belong to a nested prefab or a variant base are left to that prefab's own import.
    /// Pending prefabs and restores are kept in SessionState, so they survive Play and domain reloads, and run in Edit Mode.
    /// A restore that cannot be done is logged with its guid and id.
    /// </summary>
    internal sealed class PrefabIdStripper : AssetPostprocessor
    {
        // ==================== VARIABLES ====================

        private const string Category = "SceneRefs";
        private const string RestoresKey = "HarryTools.SceneRefs.PrefabIdStripper.Restores";

        // Not ClearOnReload: the queues must survive entering Play without a domain reload too.
        private static readonly SessionStringSet pending = new SessionStringSet("HarryTools.SceneRefs.PrefabIdStripper.Pending");
        // Restores waiting for their tick; null until loaded from SessionState after a domain reload.
        private static List<Restore> restores;

        // Paths this class is saving right now; their import is its own and is ignored.
        [ClearOnReload(AssignNewInstance = true)] private static HashSet<string> saving = new HashSet<string>(StringComparer.Ordinal);
        [ClearOnReload] private static int suspendDepth;
        [ClearOnReload] private static bool scheduled;

        // ==================== SETUP ====================

        [InitializeOnLoadMethod]
        private static void Subscribe()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            // Work queued before a domain reload (or during Play) is picked up again.
            if (HasPending) Schedule();
        }

        // ==================== PUBLIC API ====================

        /// <summary>
        /// Removes the SceneIds the prefab asset owns and gives them back to its scene instances on the next editor tick.
        /// Returns true when something was removed.
        /// </summary>
        internal static bool Strip(string _prefabPath)
        {
            if (suspendDepth > 0 || EditorApplication.isPlayingOrWillChangePlaymode || !IsProjectPrefab(_prefabPath)) return false;
            // Most prefabs never mention SceneId; they are not loaded at all.
            if (!SceneYamlReader.MayContainSceneId(_prefabPath)) return false;

            GameObject _asset = AssetDatabase.LoadAssetAtPath<GameObject>(_prefabPath);
            if (_asset == null) return false;

            HashSet<SceneId> _owned = OwnedIds(_asset);
            if (_owned.Count == 0) return false;

            List<Restore> _restores = CollectInstances(_owned, _prefabPath);
            if (!RemoveFromAsset(_prefabPath)) return false;

            ForgetPrefab(_prefabPath);
            if (_restores.Count > 0)
            {
                Restores.AddRange(_restores);
                SaveRestores();
                Schedule();
            }
            return true;
        }

        /// <summary>Holds the stripper back until disposed, for code that saves a prefab with SceneIds on purpose (tests).</summary>
        internal static IDisposable Suspend()
        {
            suspendDepth++;
            return new SuspendScope();
        }

        /// <summary>Drops the in-memory queues and live object references, as a domain reload does. Tests only.</summary>
        internal static void ForgetInMemoryForTests()
        {
            pending.ForgetInMemory();
            restores = null;
        }

        // ==================== POSTPROCESSOR ====================

        private static void OnPostprocessAllAssets(string[] _imported, string[] _deleted, string[] _moved, string[] _movedFrom)
        {
            if (suspendDepth > 0) return;

            bool _any = false;
            for (int _i = 0; _i < _imported.Length; _i++)
            {
                string _path = _imported[_i];
                if (!IsProjectPrefab(_path) || saving.Contains(_path)) continue;
                _any |= pending.Add(_path);
            }

            // Prefab contents cannot be loaded and saved while the import is still running.
            if (_any) Schedule();
        }

        // ==================== SCHEDULING ====================

        private static bool HasPending => pending.Count > 0 || Restores.Count > 0;

        private static void Schedule()
        {
            if (scheduled) return;
            scheduled = true;
            EditorTick.Next(RunPending);
        }

        private static void RunPending()
        {
            scheduled = false;
            // Left queued: OnPlayModeStateChanged schedules it again once the editor is back in Edit Mode.
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            // Restores first: they belong to strips that already happened.
            RestoreInstances();

            List<string> _paths = pending.TakeAll();
            for (int _i = 0; _i < _paths.Count; _i++)
            {
                try
                {
                    Strip(_paths[_i]);
                }
                catch (Exception _exception)
                {
                    Logger.LogWarning(Category, $"Could not remove the scene ids of prefab '{_paths[_i]}': {_exception.Message}");
                }
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange _change)
        {
            if (_change == PlayModeStateChange.EnteredEditMode && HasPending) Schedule();
        }

        // ==================== STRIP ====================

        /// <summary>SceneIds that belong to the prefab itself: no source object, so not part of a nested prefab or a variant base.</summary>
        private static HashSet<SceneId> OwnedIds(GameObject _root)
        {
            HashSet<SceneId> _owned = new HashSet<SceneId>();
            SceneId[] _ids = _root.GetComponentsInChildren<SceneId>(true);
            for (int _i = 0; _i < _ids.Length; _i++)
                if (PrefabUtility.GetCorrespondingObjectFromSource(_ids[_i]) == null) _owned.Add(_ids[_i]);
            return _owned;
        }

        /// <summary>Scene instances whose SceneId comes from one the asset owns. Only the first instance of each guid keeps it.</summary>
        private static List<Restore> CollectInstances(HashSet<SceneId> _owned, string _prefabPath)
        {
            List<Restore> _restores = new List<Restore>();
            HashSet<string> _guids = new HashSet<string>(StringComparer.Ordinal);
            int _dropped = 0;

            foreach (SceneId _instanceId in SceneRefSync.LoadedSceneIds())
            {
                if (!PrefabUtility.IsPartOfPrefabInstance(_instanceId)) continue;
                if (!(PrefabUtility.GetCorrespondingObjectFromOriginalSource(_instanceId) is SceneId _source) || !_owned.Contains(_source)) continue;

                if (!string.IsNullOrEmpty(_instanceId.Guid) && !_guids.Add(_instanceId.Guid))
                {
                    _dropped++;
                    continue;
                }

                GameObject _target = _instanceId.gameObject;
                _restores.Add(new Restore
                {
                    Target = GlobalObjectId.GetGlobalObjectIdSlow(_target).ToString(),
                    Guid = _instanceId.Guid,
                    Id = _instanceId.Id,
                    Live = _target,
                });
            }

            if (_dropped > 0)
                Logger.LogWarning(Category, $"Prefab '{_prefabPath}' had scene ids shared by several scene instances. Only the first instance of each id keeps it; {_dropped} instance(s) lost their SceneId.");
            return _restores;
        }

        private static bool RemoveFromAsset(string _prefabPath)
        {
            GameObject _contents = PrefabUtility.LoadPrefabContents(_prefabPath);
            try
            {
                HashSet<SceneId> _owned = OwnedIds(_contents);
                if (_owned.Count == 0) return false;
                foreach (SceneId _id in _owned) Object.DestroyImmediate(_id);

                saving.Add(_prefabPath);
                try
                {
                    PrefabUtility.SaveAsPrefabAsset(_contents, _prefabPath, out bool _success);
                    return _success;
                }
                finally
                {
                    saving.Remove(_prefabPath);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(_contents);
            }
        }

        /// <summary>Removes the prefab from the index list of prefabs with ids, without creating the index.</summary>
        private static void ForgetPrefab(string _prefabPath)
        {
            SceneIdIndex _index = SceneIdIndex.Existing;
            if (_index == null || !Contains(_index.PrefabsWithIds, _prefabPath)) return;

            List<string> _paths = new List<string>(_index.PrefabsWithIds);
            _paths.Remove(_prefabPath);
            _index.SetPrefabsWithIds(_paths);
        }

        // ==================== RESTORE ====================

        private static List<Restore> Restores
        {
            get
            {
                if (restores != null) return restores;
                restores = new List<Restore>();
                string _json = SessionState.GetString(RestoresKey, string.Empty);
                if (_json.Length > 0)
                {
                    RestoreList _stored = JsonUtility.FromJson<RestoreList>(_json);
                    if (_stored?.Items != null) restores.AddRange(_stored.Items);
                }
                return restores;
            }
        }

        private static void SaveRestores()
        {
            if (Restores.Count == 0) SessionState.EraseString(RestoresKey);
            else SessionState.SetString(RestoresKey, JsonUtility.ToJson(new RestoreList { Items = restores }));
        }

        /// <summary>Gives every queued instance its SceneId back. Each one that cannot get it is logged.</summary>
        private static void RestoreInstances()
        {
            if (Restores.Count == 0) return;

            List<Restore> _queued = new List<Restore>(restores);
            restores.Clear();
            SaveRestores();

            for (int _i = 0; _i < _queued.Count; _i++)
            {
                Restore _restore = _queued[_i];
                GameObject _target = Resolve(_restore);
                if (_target == null)
                {
                    Drop(_restore, "its scene object is gone or its scene is not loaded");
                    continue;
                }

                if (_target.TryGetComponent(out SceneId _existing))
                {
                    // Already back (an undo or another restore): nothing was lost.
                    if (_existing.Guid != _restore.Guid) Drop(_restore, $"'{_target.name}' already has SceneId '{_existing.Id}' ({_existing.Guid})");
                    continue;
                }

                SceneId _id = Undo.AddComponent<SceneId>(_target);
                _id.Set(_restore.Guid, _restore.Id);
                EditorUtility.SetDirty(_id);
                EditorSceneManager.MarkSceneDirty(_target.scene);
            }
        }

        private static GameObject Resolve(Restore _restore)
        {
            if (_restore.Live != null) return _restore.Live;
            if (string.IsNullOrEmpty(_restore.Target) || !GlobalObjectId.TryParse(_restore.Target, out GlobalObjectId _globalId)) return null;
            return GlobalObjectId.GlobalObjectIdentifierToObjectSlow(_globalId) as GameObject;
        }

        private static void Drop(Restore _restore, string _reason) =>
            Logger.LogWarning(Category, $"SceneId '{_restore.Id}' (guid {_restore.Guid}) was removed from its prefab asset but could not be given back to its scene instance: {_reason}. References to that guid will not resolve.");

        // ==================== HELPERS ====================

        private static bool Contains(IReadOnlyList<string> _list, string _value)
        {
            for (int _i = 0; _i < _list.Count; _i++)
                if (_list[_i] == _value) return true;
            return false;
        }

        private static bool IsProjectPrefab(string _path) =>
            !string.IsNullOrEmpty(_path)
            && _path.StartsWith("Assets/", StringComparison.Ordinal)
            && _path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);

        #region Types

        [Serializable]
        private struct Restore
        {
            // GlobalObjectId of the instance GameObject, to find it again after a domain reload.
            public string Target;
            public string Guid;
            public string Id;
            [NonSerialized] public GameObject Live;
        }

        [Serializable]
        private sealed class RestoreList
        {
            public List<Restore> Items = new List<Restore>();
        }

        private sealed class SuspendScope : IDisposable
        {
            private bool disposed;

            public void Dispose()
            {
                if (disposed) return;
                disposed = true;
                if (suspendDepth > 0) suspendDepth--;
            }
        }

        #endregion
    }
}
