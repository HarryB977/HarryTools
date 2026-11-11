using System;
using System.Collections.Generic;
using System.IO;
using HarryTools.DomainReload;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Logger = HarryTools.LoggerTool.Logger;

namespace HarryTools.SceneRefs.Editor
{
    /// <summary>
    /// Keeps <see cref="SceneIdIndex.Active"/> in sync with the scenes of the project.
    /// Saved scenes are read from memory (exact path). Scenes changed outside the editor are marked stale and read
    /// from their file on the next editor tick (light path, without component types). Does nothing during Play.
    /// </summary>
    [InitializeOnLoad]
    internal sealed class SceneIdIndexMaintainer : AssetPostprocessor
    {
        // ==================== VARIABLES ====================

        private const string Category = "SceneRefs";
        private const string ProgressTitle = "Rebuilding scene id index";

        [ClearOnReload(AssignNewInstance = true)] private static HashSet<string> pendingImported = new HashSet<string>(StringComparer.Ordinal);
        [ClearOnReload(AssignNewInstance = true)] private static HashSet<string> pendingDeleted = new HashSet<string>(StringComparer.Ordinal);
        [ClearOnReload] private static bool pendingMissingSweep;
        [ClearOnReload] private static bool scheduled;

        // ==================== EVENTS ====================

        /// <summary>Raised after a full rebuild.</summary>
        public static event Action Rebuilt;

        // ==================== SETUP ====================

        static SceneIdIndexMaintainer()
        {
            EditorSceneManager.sceneSaved -= OnSceneSaved;
            EditorSceneManager.sceneSaved += OnSceneSaved;
            // Stale marks survive domain reloads inside the loaded index; finish them once the editor is idle.
            EditorApplication.delayCall += RefreshStaleAfterReload;
        }

        // ==================== PUBLIC API ====================

        /// <summary>Reads every scene and prefab under Assets again. Asks to save modified scenes first and cancels if the user cancels.</summary>
        [MenuItem("Tools/HarryTools/SceneRefs/Rebuild Id Index")]
        public static void Rebuild() => Rebuild(true, new[] { "Assets" });

        /// <summary>Reads the scenes marked as stale again.</summary>
        public static void RefreshStale() => RefreshStale(false);

        /// <summary>Reads stale scenes again. With _allowPlayTransition it also runs while Play is about to start, for the Play guard.</summary>
        internal static void RefreshStale(bool _allowPlayTransition)
        {
            if (EditorApplication.isPlaying || (!_allowPlayTransition && EditorApplication.isPlayingOrWillChangePlaymode)) return;
            SceneIdIndex _index = SceneIdIndex.Existing;
            if (_index == null || _index.StaleScenes.Count == 0) return;

            List<string> _stale = new List<string>(_index.StaleScenes);
            _index.BeginBatch();
            try
            {
                for (int _i = 0; _i < _stale.Count; _i++) SafeReindex(_index, _stale[_i]);
            }
            finally
            {
                _index.EndBatch();
            }
        }

        // ==================== INTERNAL ====================

        /// <summary>Rebuild without the save prompt and limited to some folders, for tests. Returns false when cancelled.</summary>
        internal static bool Rebuild(bool _askToSave, string[] _folders)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Logger.LogWarning(Category, "The scene id index cannot be rebuilt during Play.");
                return false;
            }

            if (_askToSave && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;

            SceneIdIndex _index = SceneIdIndex.Active;
            if (_index == null) return false;
            HashSet<string> _found = new HashSet<string>(StringComparer.Ordinal);
            List<string> _prefabs = new List<string>();
            _index.BeginBatch();
            try
            {
                string[] _sceneGuids = AssetDatabase.FindAssets("t:Scene", _folders);
                string[] _prefabGuids = AssetDatabase.FindAssets("t:Prefab", _folders);
                float _total = Mathf.Max(1, _sceneGuids.Length + _prefabGuids.Length);

                for (int _i = 0; _i < _sceneGuids.Length; _i++)
                {
                    string _path = AssetDatabase.GUIDToAssetPath(_sceneGuids[_i]);
                    if (!IsProjectScene(_path)) continue;

                    EditorUtility.DisplayProgressBar(ProgressTitle, _path, _i / _total);
                    // A scene that cannot be read keeps its old entries and stays stale; it is not removed.
                    _found.Add(_sceneGuids[_i]);
                    try
                    {
                        _index.ReplaceScene(_sceneGuids[_i], ReadScene(_path));
                    }
                    catch (Exception _exception)
                    {
                        _index.MarkStale(_sceneGuids[_i]);
                        Logger.LogWarning(Category, $"Could not index scene '{_path}': {_exception.Message}");
                    }
                }

                List<string> _known = _index.SceneGuids();
                _known.AddRange(_index.StaleScenes);
                for (int _i = 0; _i < _known.Count; _i++)
                    if (!_found.Contains(_known[_i])) _index.RemoveScene(_known[_i]);

                for (int _i = 0; _i < _prefabGuids.Length; _i++)
                {
                    string _path = AssetDatabase.GUIDToAssetPath(_prefabGuids[_i]);
                    // t:Prefab also matches model files; only real prefab assets can hold a SceneId.
                    if (!_path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)) continue;

                    EditorUtility.DisplayProgressBar(ProgressTitle, _path, (_sceneGuids.Length + _i) / _total);
                    try
                    {
                        if (PrefabHasIds(_path)) _prefabs.Add(_path);
                    }
                    catch (Exception _exception)
                    {
                        Logger.LogWarning(Category, $"Could not check prefab '{_path}' for scene ids: {_exception.Message}");
                    }
                }

                _index.SetPrefabsWithIds(_prefabs);
            }
            finally
            {
                _index.EndBatch();
                EditorUtility.ClearProgressBar();
            }

            Rebuilt?.Invoke();
            return true;
        }

        /// <summary>Runs the queued postprocessor work now instead of on the next editor tick.</summary>
        internal static void FlushPending() => ProcessPending();

        // ==================== SCENE SAVED ====================

        private static void OnSceneSaved(Scene _scene)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !IsProjectScene(_scene.path)) return;

            List<SceneIdIndexEntry> _entries = SceneIndexer.FromLoadedScene(_scene);
            SceneIdIndex _index = SceneIdIndex.Existing;
            // The index asset is only created once there is something to store.
            if (_index == null && (_entries.Count == 0 || SceneIdIndex.IsOverridden)) return;
            if (_index == null) _index = SceneIdIndex.Active;

            _index.BeginBatch();
            _index.RemoveScene(SceneIndexer.UnsavedPrefix + _scene.handle);
            _index.ReplaceScene(SceneIndexer.SceneKey(_scene), _entries);
            _index.EndBatch();
        }

        // ==================== POSTPROCESSOR ====================

        private static void OnPostprocessAllAssets(string[] _imported, string[] _deleted, string[] _moved, string[] _movedFrom)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            bool _any = false;
            for (int _i = 0; _i < _imported.Length; _i++)
                if (IsProjectScene(_imported[_i])) _any |= QueueImported(_imported[_i]);
            for (int _i = 0; _i < _deleted.Length; _i++)
                if (IsProjectScene(_deleted[_i])) _any |= QueueDeleted(_deleted[_i]);

            // A move keeps the scene guid, so only moves into or out of Assets matter.
            for (int _i = 0; _i < _moved.Length; _i++)
            {
                bool _isNow = IsProjectScene(_moved[_i]);
                bool _was = IsProjectScene(_movedFrom[_i]);
                if (_isNow && !_was) _any |= QueueImported(_moved[_i]);
                else if (!_isNow && _was) _any |= QueueDeleted(_moved[_i]);
            }

            if (!_any || scheduled) return;
            scheduled = true;
            EditorApplication.delayCall += RunPending;
        }

        private static bool QueueImported(string _path)
        {
            string _guid = AssetDatabase.AssetPathToGUID(_path);
            if (string.IsNullOrEmpty(_guid)) return false;

            pendingImported.Add(_guid);
            pendingDeleted.Remove(_guid);
            // Only an index that is already loaded is marked here; loading assets during an import is avoided.
            SceneIdIndex.Loaded?.MarkStale(_guid);
            return true;
        }

        private static bool QueueDeleted(string _path)
        {
            string _guid = AssetDatabase.AssetPathToGUID(_path, AssetPathToGUIDOptions.IncludeRecentlyDeletedAssets);
            if (string.IsNullOrEmpty(_guid)) pendingMissingSweep = true;
            else
            {
                pendingDeleted.Add(_guid);
                pendingImported.Remove(_guid);
            }

            return true;
        }

        private static void RunPending()
        {
            scheduled = false;
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            ProcessPending();
        }

        private static void ProcessPending()
        {
            if (pendingImported.Count == 0 && pendingDeleted.Count == 0 && !pendingMissingSweep) return;

            List<string> _imported = new List<string>(pendingImported);
            List<string> _deleted = new List<string>(pendingDeleted);
            bool _sweep = pendingMissingSweep;
            pendingImported.Clear();
            pendingDeleted.Clear();
            pendingMissingSweep = false;

            SceneIdIndex _index = SceneIdIndex.Existing;
            if (_index == null && SceneIdIndex.IsOverridden) return;
            if (_index == null)
            {
                // No index yet: create it only if an imported scene has ids.
                bool _hasIds = false;
                for (int _i = 0; _i < _imported.Count && !_hasIds; _i++)
                    _hasIds = HasIds(AssetDatabase.GUIDToAssetPath(_imported[_i]));
                if (!_hasIds) return;
                _index = SceneIdIndex.Active;
            }

            _index.BeginBatch();
            try
            {
                for (int _i = 0; _i < _deleted.Count; _i++) _index.RemoveScene(_deleted[_i]);
                if (_sweep) RemoveMissingScenes(_index);
                for (int _i = 0; _i < _imported.Count; _i++)
                {
                    _index.MarkStale(_imported[_i]);
                    SafeReindex(_index, _imported[_i]);
                }
            }
            finally
            {
                _index.EndBatch();
            }
        }

        private static void RefreshStaleAfterReload()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            // Do not create the asset: only an existing index can have stale scenes.
            if (!File.Exists(SceneIdIndex.DefaultPath) && SceneIdIndex.Loaded == null) return;
            RefreshStale();
        }

        // ==================== READING ====================

        /// <summary>Reindex that never stops the caller: a scene that throws stays stale.</summary>
        private static void SafeReindex(SceneIdIndex _index, string _sceneGuid)
        {
            try
            {
                Reindex(_index, _sceneGuid);
            }
            catch (Exception _exception)
            {
                _index.MarkStale(_sceneGuid);
                Logger.LogWarning(Category, $"Could not index scene '{AssetDatabase.GUIDToAssetPath(_sceneGuid)}': {_exception.Message}");
            }
        }

        /// <summary>Reads one stale scene again. Scenes that cannot be read stay stale until Rebuild.</summary>
        private static void Reindex(SceneIdIndex _index, string _sceneGuid)
        {
            if (_sceneGuid.StartsWith(SceneIndexer.UnsavedPrefix, StringComparison.Ordinal))
            {
                Scene _unsaved = LoadedUnsavedScene(_sceneGuid);
                if (_unsaved.IsValid()) _index.ReplaceScene(_sceneGuid, SceneIndexer.FromLoadedScene(_unsaved));
                else _index.RemoveScene(_sceneGuid);
                return;
            }

            string _path = AssetDatabase.GUIDToAssetPath(_sceneGuid);
            if (string.IsNullOrEmpty(_path) || !File.Exists(_path))
            {
                _index.RemoveScene(_sceneGuid);
                return;
            }

            // A loaded scene without unsaved changes matches its file, so the exact path is used.
            Scene _loaded = SceneManager.GetSceneByPath(_path);
            if (_loaded.IsValid() && _loaded.isLoaded && !_loaded.isDirty)
            {
                _index.ReplaceScene(_sceneGuid, SceneIndexer.FromLoadedScene(_loaded));
                return;
            }

            if (SceneYamlReader.TryFromFile(_path, out List<SceneIdIndexEntry> _entries)) _index.ReplaceScene(_sceneGuid, _entries);
            else Logger.LogWarning(Category, $"Scene '{_path}' is not saved as text and could not be indexed. Use Rebuild Index.");
        }

        /// <summary>Exact read of a scene asset, opening it additively when it is not loaded.</summary>
        private static List<SceneIdIndexEntry> ReadScene(string _path)
        {
            if (string.IsNullOrEmpty(_path)) return new List<SceneIdIndexEntry>();

            Scene _scene = SceneManager.GetSceneByPath(_path);
            if (_scene.IsValid() && _scene.isLoaded) return SceneIndexer.FromLoadedScene(_scene);

            // A scene file that never mentions SceneId is not opened.
            if (!SceneYamlReader.MayContainSceneId(_path)) return new List<SceneIdIndexEntry>();

            // A scene shown unloaded in the hierarchy is unloaded again afterwards instead of removed.
            bool _wasInHierarchy = _scene.IsValid();
            Scene _opened = default;
            try
            {
                _opened = EditorSceneManager.OpenScene(_path, OpenSceneMode.Additive);
                if (!_opened.IsValid() || !_opened.isLoaded) throw new InvalidOperationException("the scene could not be opened.");
                return SceneIndexer.FromLoadedScene(_opened);
            }
            finally
            {
                if (_opened.IsValid()) EditorSceneManager.CloseScene(_opened, !_wasInHierarchy);
            }
        }

        private static bool HasIds(string _path)
        {
            Scene _loaded = SceneManager.GetSceneByPath(_path);
            if (_loaded.IsValid() && _loaded.isLoaded) return SceneIndexer.FromLoadedScene(_loaded).Count > 0;
            return SceneYamlReader.FromFile(_path).Count > 0;
        }

        private static bool PrefabHasIds(string _path)
        {
            if (!SceneYamlReader.MayContainSceneId(_path)) return false;

            GameObject _root = null;
            try
            {
                _root = PrefabUtility.LoadPrefabContents(_path);
                return _root != null && _root.GetComponentsInChildren<SceneId>(true).Length > 0;
            }
            finally
            {
                if (_root != null) PrefabUtility.UnloadPrefabContents(_root);
            }
        }

        private static void RemoveMissingScenes(SceneIdIndex _index)
        {
            List<string> _known = _index.SceneGuids();
            _known.AddRange(_index.StaleScenes);
            for (int _i = 0; _i < _known.Count; _i++)
            {
                if (_known[_i].StartsWith(SceneIndexer.UnsavedPrefix, StringComparison.Ordinal)) continue;
                string _path = AssetDatabase.GUIDToAssetPath(_known[_i]);
                if (string.IsNullOrEmpty(_path) || !File.Exists(_path)) _index.RemoveScene(_known[_i]);
            }
        }

        private static Scene LoadedUnsavedScene(string _sceneGuid)
        {
            for (int _i = 0; _i < SceneManager.sceneCount; _i++)
            {
                Scene _scene = SceneManager.GetSceneAt(_i);
                if (_scene.isLoaded && string.IsNullOrEmpty(_scene.path) && SceneIndexer.SceneKey(_scene) == _sceneGuid) return _scene;
            }

            return default;
        }

        private static bool IsProjectScene(string _path) =>
            !string.IsNullOrEmpty(_path)
            && _path.StartsWith("Assets/", StringComparison.Ordinal)
            && _path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);
    }
}
