using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarryTools.DomainReload;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Logger = HarryTools.LoggerTool.Logger;

namespace HarryTools.Flags.Editor
{
    /// <summary>
    /// Keeps the <see cref="FlagUsageIndex"/> in sync with the project. Saved and loaded scenes are read from memory (exact);
    /// scenes changed while closed (git pull) are searched as text for known flag guids (approximate) until they are saved or
    /// rebuilt; prefabs and ScriptableObjects are loaded and read exactly. Imports are queued in SessionState at any time and
    /// processed on the next editor tick in Edit Mode. Does nothing while the project has no <see cref="FlagDatabase"/>.
    /// </summary>
    [InitializeOnLoad]
    internal sealed class FlagUsageMaintainer : AssetPostprocessor
    {
        // ==================== VARIABLES ====================

        private const string Category = "Flags";
        private const string ProgressTitle = "Rebuilding flag usage index";

        // Not ClearOnReload: the queue must survive entering Play without a domain reload too.
        private const string ImportedScenesKey = "HarryTools.Flags.UsageMaintainer.ImportedScenes";
        private const string ImportedAssetsKey = "HarryTools.Flags.UsageMaintainer.ImportedAssets";
        private const string DeletedAssetsKey = "HarryTools.Flags.UsageMaintainer.DeletedAssets";
        private const string SweepScenesKey = "HarryTools.Flags.UsageMaintainer.SweepScenes";
        private const string SceneNamesKey = "HarryTools.Flags.UsageMaintainer.SceneNames";

        [ClearOnReload] private static bool scheduled;

        private static bool HasPending =>
            !string.IsNullOrEmpty(SessionState.GetString(ImportedScenesKey, string.Empty))
            || !string.IsNullOrEmpty(SessionState.GetString(ImportedAssetsKey, string.Empty))
            || !string.IsNullOrEmpty(SessionState.GetString(DeletedAssetsKey, string.Empty))
            || SessionState.GetBool(SweepScenesKey, false)
            || SessionState.GetBool(SceneNamesKey, false);

        // ==================== EVENTS ====================

        /// <summary>Raised after the index may have changed.</summary>
        public static event Action Changed;

        // ==================== SETUP ====================

        static FlagUsageMaintainer()
        {
            EditorSceneManager.sceneSaved -= OnSceneSaved;
            EditorSceneManager.sceneSaved += OnSceneSaved;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            // Imports queued before a domain reload (or during Play) are picked up again.
            if (HasPending) Schedule();
        }

        // ==================== PUBLIC API ====================

        /// <summary>Reads every scene, prefab and ScriptableObject under Assets again, opening closed scenes additively.</summary>
        [MenuItem("Tools/HarryTools/Flags/Rebuild Usage Index")]
        public static void Rebuild()
        {
            if (FlagDatabaseLocator.Find() == null)
            {
                Logger.Log(Category, "The project has no flag database, so there is no usage index to rebuild.");
                return;
            }
            Rebuild(new[] { "Assets" });
        }

        /// <summary>
        /// Reads the loaded scenes from memory, unsaved changes included, into the index. Scenes that were never saved are skipped:
        /// they cannot own local flags and are indexed once saved.
        /// </summary>
        public static void RefreshLoadedScenes()
        {
            // Runs while Play is about to start, for the Play guard, but not in Play.
            if (EditorApplication.isPlaying || FlagDatabaseLocator.Find() == null) return;

            List<KeyValuePair<string, List<FlagUsageEntry>>> _read = new List<KeyValuePair<string, List<FlagUsageEntry>>>();
            for (int _i = 0; _i < SceneManager.sceneCount; _i++)
            {
                Scene _scene = SceneManager.GetSceneAt(_i);
                if (!_scene.isLoaded || !IsProjectScene(_scene.path)) continue;
                _read.Add(new KeyValuePair<string, List<FlagUsageEntry>>(AssetDatabase.AssetPathToGUID(_scene.path), FlagUsageIndexer.FromLoadedScene(_scene)));
            }

            FlagUsageIndex _index = IndexFor(_read.Any(_pair => _pair.Value.Count > 0));
            if (_index == null) return;

            _index.BeginBatch();
            try
            {
                foreach (KeyValuePair<string, List<FlagUsageEntry>> _pair in _read) _index.ReplaceScene(_pair.Key, _pair.Value);
            }
            finally
            {
                _index.EndBatch();
            }
            Changed?.Invoke();
        }

        // ==================== INTERNAL ====================

        /// <summary>Rebuild limited to some folders, for tests. Returns false when it did not run.</summary>
        internal static bool Rebuild(string[] _folders)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Logger.LogWarning(Category, "The flag usage index cannot be rebuilt during Play.");
                return false;
            }
            if (FlagDatabaseLocator.Find() == null) return false;

            FlagUsageIndex _index = FlagUsageIndexLocator.GetOrCreate();
            _index.BeginBatch();
            try
            {
                string[] _sceneGuids = AssetDatabase.FindAssets("t:Scene", _folders);
                List<string> _assetPaths = AssetDatabase.FindAssets("t:Prefab", _folders)
                    .Concat(AssetDatabase.FindAssets("t:ScriptableObject", _folders))
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(IsIndexableAsset)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                float _total = Mathf.Max(1, _sceneGuids.Length + _assetPaths.Count);

                HashSet<string> _foundScenes = new HashSet<string>(StringComparer.Ordinal);
                for (int _i = 0; _i < _sceneGuids.Length; _i++)
                {
                    string _path = AssetDatabase.GUIDToAssetPath(_sceneGuids[_i]);
                    if (!IsProjectScene(_path)) continue;

                    EditorUtility.DisplayProgressBar(ProgressTitle, _path, _i / _total);
                    _foundScenes.Add(_sceneGuids[_i]);
                    // A scene that cannot be read keeps its old entries.
                    try
                    {
                        _index.ReplaceScene(_sceneGuids[_i], ReadScene(_path));
                    }
                    catch (Exception _exception)
                    {
                        Logger.LogWarning(Category, $"Could not index the flag usages of scene '{_path}': {_exception.Message}");
                    }
                }

                foreach (string _sceneGuid in _index.SceneGuids())
                {
                    if (_foundScenes.Contains(_sceneGuid)) continue;
                    string _path = AssetDatabase.GUIDToAssetPath(_sceneGuid);
                    if (IsMissingScene(_path) || IsUnder(_path, _folders)) _index.RemoveScene(_sceneGuid);
                }

                HashSet<string> _foundAssets = new HashSet<string>(_assetPaths, StringComparer.Ordinal);
                for (int _i = 0; _i < _assetPaths.Count; _i++)
                {
                    EditorUtility.DisplayProgressBar(ProgressTitle, _assetPaths[_i], (_sceneGuids.Length + _i) / _total);
                    SafeReindexAsset(_index, _assetPaths[_i]);
                }

                foreach (string _path in _index.AssetPaths())
                    if (!_foundAssets.Contains(_path) && (!File.Exists(_path) || IsUnder(_path, _folders))) _index.RemoveAsset(_path);
            }
            finally
            {
                _index.EndBatch();
                EditorUtility.ClearProgressBar();
            }

            Changed?.Invoke();
            return true;
        }

        /// <summary>Runs the queued postprocessor work now instead of on the next editor tick.</summary>
        internal static void FlushPending() => ProcessPending();

        // ==================== SCENE SAVED ====================

        private static void OnSceneSaved(Scene _scene)
        {
            // Scenes cannot be saved in Play; a save while Play is about to start ("save on Play" scripts) is still indexed.
            if (EditorApplication.isPlaying || !IsProjectScene(_scene.path) || FlagDatabaseLocator.Find() == null) return;

            List<FlagUsageEntry> _entries = FlagUsageIndexer.FromLoadedScene(_scene);
            // The index asset is only created once there is something to store.
            FlagUsageIndex _index = IndexFor(_entries.Count > 0);
            if (_index == null) return;

            _index.ReplaceScene(AssetDatabase.AssetPathToGUID(_scene.path), _entries);
            Changed?.Invoke();
        }

        // ==================== POSTPROCESSOR ====================

        private static void OnPostprocessAllAssets(string[] _imported, string[] _deleted, string[] _moved, string[] _movedFrom)
        {
            // Queued even during Play; the work itself waits for Edit Mode. Nothing is loaded here.
            HashSet<string> _scenes = Load(ImportedScenesKey);
            HashSet<string> _assetsIn = Load(ImportedAssetsKey);
            HashSet<string> _assetsOut = Load(DeletedAssetsKey);
            bool _sweep = false;
            bool _names = false;

            for (int _i = 0; _i < _imported.Length; _i++)
            {
                if (IsProjectScene(_imported[_i])) QueueScene(_scenes, _imported[_i]);
                else if (IsIndexableAsset(_imported[_i])) Move(_imported[_i], _assetsOut, _assetsIn);
            }

            for (int _i = 0; _i < _deleted.Length; _i++)
            {
                if (IsProjectScene(_deleted[_i])) _sweep = true;
                else if (IsIndexableAsset(_deleted[_i])) Move(_deleted[_i], _assetsIn, _assetsOut);
            }

            for (int _i = 0; _i < _moved.Length; _i++)
            {
                bool _isScene = IsProjectScene(_moved[_i]);
                bool _wasScene = IsProjectScene(_movedFrom[_i]);
                if (_isScene || _wasScene)
                {
                    // A move keeps the scene guid: the entries stay valid, only the cached scene names change.
                    _names = true;
                    if (_isScene && !_wasScene) QueueScene(_scenes, _moved[_i]);
                    else if (!_isScene) _sweep = true;
                    continue;
                }

                if (IsIndexableAsset(_movedFrom[_i])) Move(_movedFrom[_i], _assetsIn, _assetsOut);
                if (IsIndexableAsset(_moved[_i])) Move(_moved[_i], _assetsOut, _assetsIn);
            }

            Save(ImportedScenesKey, _scenes);
            Save(ImportedAssetsKey, _assetsIn);
            Save(DeletedAssetsKey, _assetsOut);
            if (_sweep) SessionState.SetBool(SweepScenesKey, true);
            if (_names) SessionState.SetBool(SceneNamesKey, true);

            if (HasPending) Schedule();
        }

        private static void QueueScene(HashSet<string> _scenes, string _path)
        {
            string _guid = AssetDatabase.AssetPathToGUID(_path);
            if (!string.IsNullOrEmpty(_guid)) _scenes.Add(_guid);
        }

        private static void Move(string _value, HashSet<string> _from, HashSet<string> _to)
        {
            _from.Remove(_value);
            _to.Add(_value);
        }

        private static void Schedule()
        {
            if (scheduled) return;
            scheduled = true;

            // EditorApplication.delayCall does not run while the editor is in the background, which leaves queued work undone
            // during test runs; a one-shot update callback does.
            EditorApplication.CallbackFunction _tick = null;
            _tick = () =>
            {
                EditorApplication.update -= _tick;
                RunPending();
            };
            EditorApplication.update += _tick;
        }

        private static void RunPending()
        {
            scheduled = false;
            // Left queued: OnPlayModeStateChanged schedules it again once the editor is back in Edit Mode.
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            ProcessPending();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange _change)
        {
            if (_change == PlayModeStateChange.EnteredEditMode && HasPending) Schedule();
        }

        private static void ProcessPending()
        {
            if (!HasPending) return;

            HashSet<string> _scenes = Take(ImportedScenesKey);
            HashSet<string> _assetsIn = Take(ImportedAssetsKey);
            HashSet<string> _assetsOut = Take(DeletedAssetsKey);
            bool _sweep = SessionState.GetBool(SweepScenesKey, false);
            bool _names = SessionState.GetBool(SceneNamesKey, false);
            SessionState.EraseBool(SweepScenesKey);
            SessionState.EraseBool(SceneNamesKey);

            FlagDatabase _database = FlagDatabaseLocator.Find();
            if (_database == null) return;
            if (_names) FlagDatabaseEditing.RefreshSceneNames(_database);

            List<string> _known = _database.Flags.Where(_flag => _flag != null).Select(_flag => _flag.Guid).ToList();
            List<KeyValuePair<string, List<FlagUsageEntry>>> _sceneReads = new List<KeyValuePair<string, List<FlagUsageEntry>>>();
            foreach (string _sceneGuid in _scenes)
            {
                string _path = AssetDatabase.GUIDToAssetPath(_sceneGuid);
                if (IsMissingScene(_path)) continue; // Removed by the sweep below.
                try
                {
                    _sceneReads.Add(new KeyValuePair<string, List<FlagUsageEntry>>(_sceneGuid, ReadImportedScene(_path, _known)));
                }
                catch (Exception _exception)
                {
                    Logger.LogWarning(Category, $"Could not index the flag usages of scene '{_path}': {_exception.Message}");
                }
            }

            // The index asset is only created once there is something to store.
            bool _hasEntries = _sceneReads.Any(_pair => _pair.Value.Count > 0) || _assetsIn.Any(FlagUsageIndexer.MayContainFlagRef);
            FlagUsageIndex _index = IndexFor(_hasEntries);
            if (_index == null) return;

            _index.BeginBatch();
            try
            {
                foreach (string _path in _assetsOut) _index.RemoveAsset(_path);
                if (_sweep || _scenes.Count > 0) RemoveMissingScenes(_index);
                foreach (KeyValuePair<string, List<FlagUsageEntry>> _pair in _sceneReads) _index.ReplaceScene(_pair.Key, _pair.Value);
                foreach (string _path in _assetsIn)
                {
                    if (File.Exists(_path)) SafeReindexAsset(_index, _path);
                    else _index.RemoveAsset(_path);
                }
            }
            finally
            {
                _index.EndBatch();
            }
            Changed?.Invoke();
        }

        // ==================== READING ====================

        /// <summary>Exact read of a scene, opening it additively when it is not loaded.</summary>
        private static List<FlagUsageEntry> ReadScene(string _path)
        {
            Scene _scene = SceneManager.GetSceneByPath(_path);
            if (_scene.IsValid() && _scene.isLoaded) return FlagUsageIndexer.FromLoadedScene(_scene);

            // A scene file without any assigned FlagRef is not opened.
            if (!FlagUsageIndexer.MayContainFlagRef(_path)) return new List<FlagUsageEntry>();

            // A scene shown unloaded in the hierarchy is unloaded again afterwards instead of removed.
            bool _wasInHierarchy = _scene.IsValid();
            Scene _opened = default;
            try
            {
                _opened = EditorSceneManager.OpenScene(_path, OpenSceneMode.Additive);
                if (!_opened.IsValid() || !_opened.isLoaded) throw new InvalidOperationException("the scene could not be opened.");
                return FlagUsageIndexer.FromLoadedScene(_opened);
            }
            finally
            {
                if (_opened.IsValid()) EditorSceneManager.CloseScene(_opened, !_wasInHierarchy);
            }
        }

        /// <summary>A loaded scene without unsaved changes matches its file and is read exactly; otherwise the file is searched.</summary>
        private static List<FlagUsageEntry> ReadImportedScene(string _path, ICollection<string> _knownFlagGuids)
        {
            Scene _loaded = SceneManager.GetSceneByPath(_path);
            if (_loaded.IsValid() && _loaded.isLoaded && !_loaded.isDirty) return FlagUsageIndexer.FromLoadedScene(_loaded);
            return FlagUsageIndexer.FromSceneYaml(_path, _knownFlagGuids);
        }

        /// <summary>Reindex that never stops the caller: an asset that throws keeps its old entries.</summary>
        private static void SafeReindexAsset(FlagUsageIndex _index, string _path)
        {
            try
            {
                List<FlagUsageEntry> _entries = FlagUsageIndexer.MayContainFlagRef(_path) ? FlagUsageIndexer.FromAsset(_path) : new List<FlagUsageEntry>();
                _index.ReplaceAsset(_path, _entries);
            }
            catch (Exception _exception)
            {
                Logger.LogWarning(Category, $"Could not index the flag usages of asset '{_path}': {_exception.Message}");
            }
        }

        private static void RemoveMissingScenes(FlagUsageIndex _index)
        {
            foreach (string _sceneGuid in _index.SceneGuids())
                if (IsMissingScene(AssetDatabase.GUIDToAssetPath(_sceneGuid))) _index.RemoveScene(_sceneGuid);
        }

        /// <summary>The index, creating the asset only when there is something to store.</summary>
        private static FlagUsageIndex IndexFor(bool _hasEntries)
        {
            FlagUsageIndex _existing = FlagUsageIndexLocator.Find();
            if (_existing != null) return _existing;
            return _hasEntries ? FlagUsageIndexLocator.GetOrCreate() : null;
        }

        // ==================== QUEUE ====================

        private static HashSet<string> Load(string _key)
        {
            string _value = SessionState.GetString(_key, string.Empty);
            return new HashSet<string>(_value.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
        }

        private static void Save(string _key, HashSet<string> _values)
        {
            if (_values.Count == 0) SessionState.EraseString(_key);
            else SessionState.SetString(_key, string.Join("\n", _values));
        }

        private static HashSet<string> Take(string _key)
        {
            HashSet<string> _values = Load(_key);
            SessionState.EraseString(_key);
            return _values;
        }

        // ==================== PATHS ====================

        private static bool IsProjectScene(string _path) =>
            !string.IsNullOrEmpty(_path)
            && _path.StartsWith("Assets/", StringComparison.Ordinal)
            && _path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);

        private static bool IsMissingScene(string _path) => !IsProjectScene(_path) || !File.Exists(_path);

        private static bool IsIndexableAsset(string _path) =>
            !string.IsNullOrEmpty(_path)
            && _path.StartsWith("Assets/", StringComparison.Ordinal)
            && (_path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) || _path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            && _path != FlagUsageIndexLocator.AssetPath;

        private static bool IsUnder(string _path, string[] _folders)
        {
            if (string.IsNullOrEmpty(_path) || _folders == null) return false;
            for (int _i = 0; _i < _folders.Length; _i++)
                if (_path.StartsWith(_folders[_i].TrimEnd('/') + "/", StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
