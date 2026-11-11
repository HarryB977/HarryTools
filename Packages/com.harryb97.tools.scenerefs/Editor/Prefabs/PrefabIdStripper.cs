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
    /// SceneIds that belong to a nested prefab or a variant base are left to that prefab's own import. Does nothing during Play.
    /// </summary>
    internal sealed class PrefabIdStripper : AssetPostprocessor
    {
        // ==================== VARIABLES ====================

        private const string Category = "SceneRefs";

        [ClearOnReload(AssignNewInstance = true)] private static HashSet<string> pending = new HashSet<string>(StringComparer.Ordinal);
        // Paths this class is saving right now; their import is its own and is ignored.
        [ClearOnReload(AssignNewInstance = true)] private static HashSet<string> saving = new HashSet<string>(StringComparer.Ordinal);
        [ClearOnReload] private static int suspendDepth;
        [ClearOnReload] private static bool scheduled;

        // ==================== PUBLIC API ====================

        /// <summary>
        /// Removes the SceneIds the prefab asset owns and gives them back to its scene instances on the next editor tick.
        /// Returns true when something was removed.
        /// </summary>
        internal static bool Strip(string _prefabPath)
        {
            if (suspendDepth > 0 || EditorApplication.isPlayingOrWillChangePlaymode || !IsProjectPrefab(_prefabPath)) return false;

            GameObject _asset = AssetDatabase.LoadAssetAtPath<GameObject>(_prefabPath);
            if (_asset == null) return false;

            HashSet<SceneId> _owned = OwnedIds(_asset);
            if (_owned.Count == 0) return false;

            List<Restore> _restores = CollectInstances(_owned, _prefabPath);
            if (!RemoveFromAsset(_prefabPath)) return false;

            ForgetPrefab(_prefabPath);
            if (_restores.Count > 0) OnNextTick(() => RestoreInstances(_restores));
            return true;
        }

        /// <summary>Holds the stripper back until disposed, for code that saves a prefab with SceneIds on purpose (tests).</summary>
        internal static IDisposable Suspend()
        {
            suspendDepth++;
            return new SuspendScope();
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

            if (!_any || scheduled) return;
            scheduled = true;
            // Prefab contents cannot be loaded and saved while the import is still running.
            OnNextTick(RunPending);
        }

        private static void RunPending()
        {
            scheduled = false;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                // Kept for when the editor is back in Edit Mode.
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
                return;
            }

            List<string> _paths = new List<string>(pending);
            pending.Clear();
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
            if (_change != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            if (pending.Count == 0 || scheduled) return;
            scheduled = true;
            OnNextTick(RunPending);
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

                _restores.Add(new Restore { Target = _instanceId.gameObject, Guid = _instanceId.Guid, Id = _instanceId.Id });
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

        private static void RestoreInstances(List<Restore> _restores)
        {
            if (EditorApplication.isPlaying) return;

            for (int _i = 0; _i < _restores.Count; _i++)
            {
                Restore _restore = _restores[_i];
                // The object may have been deleted, or still have a SceneId from somewhere else.
                if (_restore.Target == null || _restore.Target.TryGetComponent(out SceneId _)) continue;

                SceneId _id = Undo.AddComponent<SceneId>(_restore.Target);
                _id.Set(_restore.Guid, _restore.Id);
                EditorUtility.SetDirty(_id);
                EditorSceneManager.MarkSceneDirty(_restore.Target.scene);
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

        // ==================== HELPERS ====================

        /// <summary>
        /// Runs the action once on the next editor update. EditorApplication.delayCall is not used because it does not run
        /// while the editor is in the background, which left prefabs unstripped during test runs.
        /// </summary>
        private static void OnNextTick(Action _action)
        {
            EditorApplication.CallbackFunction _tick = null;
            _tick = () =>
            {
                EditorApplication.update -= _tick;
                _action();
            };
            EditorApplication.update += _tick;
        }

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

        private struct Restore
        {
            public GameObject Target;
            public string Guid;
            public string Id;
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
