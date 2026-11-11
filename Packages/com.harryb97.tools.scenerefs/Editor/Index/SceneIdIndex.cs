using System;
using System.Collections.Generic;
using System.IO;
using HarryTools.DomainReload;
using UnityEditor;
using UnityEngine;
using Logger = HarryTools.LoggerTool.Logger;

namespace HarryTools.SceneRefs.Editor
{
    /// <summary>
    /// Editor-only cache of every SceneId in the project's scenes, plus the prefabs that wrongly contain one.
    /// Everything in it can be rebuilt from the scenes. Entries are kept sorted by scene and guid so diffs stay small.
    /// </summary>
    internal sealed class SceneIdIndex : ScriptableObject, ISerializationCallbackReceiver
    {
        // ==================== VARIABLES ====================

        private const string Category = "SceneRefs";

        internal const string DefaultFolder = "Assets/HarryTools/SceneRefs";
        internal const string DefaultPath = DefaultFolder + "/SceneIdIndex.asset";

        [SerializeField] private List<SceneIdIndexEntry> entries = new List<SceneIdIndexEntry>();
        [SerializeField] private List<string> prefabsWithIds = new List<string>();
        [SerializeField] private List<string> staleScenes = new List<string>();

        [ClearOnReload] private static SceneIdIndex overrideIndex;
        [ClearOnReload] private static bool overridden;
        [ClearOnReload] private static SceneIdIndex cached;

        [NonSerialized] private int batchDepth;
        [NonSerialized] private bool saveDeferred;

        // ==================== PROPERTIES ====================

        public IReadOnlyList<SceneIdIndexEntry> Entries => entries;

        /// <summary>Asset paths of the prefabs that contain a SceneId.</summary>
        public IReadOnlyList<string> PrefabsWithIds => prefabsWithIds;

        /// <summary>Scene guids whose entries may be out of date and must be read again.</summary>
        public IReadOnlyCollection<string> StaleScenes => staleScenes;

        /// <summary>
        /// The index in use. Loads the project index, creating it on first use.
        /// Tests assign an in-memory instance; assigning null goes back to the project index.
        /// While an override is set the project asset is never loaded or created, even if the override was destroyed (then this is null).
        /// </summary>
        public static SceneIdIndex Active
        {
            get
            {
                if (overridden) return overrideIndex;
                SceneIdIndex _existing = Existing;
                return _existing != null ? _existing : CreateProjectIndex();
            }
            set
            {
                overrideIndex = value;
                overridden = !ReferenceEquals(value, null);
            }
        }

        /// <summary>True while a test index replaces the project index.</summary>
        internal static bool IsOverridden => overridden;

        /// <summary>The index in use without creating the asset. Null when the project has none yet.</summary>
        internal static SceneIdIndex Existing
        {
            get
            {
                if (overridden) return overrideIndex;
                if (cached == null) cached = AssetDatabase.LoadAssetAtPath<SceneIdIndex>(DefaultPath);
                return cached;
            }
        }

        /// <summary>The index in use only if it is already in memory. Never loads or creates the asset.</summary>
        internal static SceneIdIndex Loaded => overridden ? overrideIndex : cached;

        // ==================== PUBLIC API ====================

        /// <summary>Replaces every entry of a scene and clears its stale mark.</summary>
        public void ReplaceScene(string _sceneGuid, List<SceneIdIndexEntry> _entries)
        {
            if (string.IsNullOrEmpty(_sceneGuid)) return;

            List<SceneIdIndexEntry> _incoming = new List<SceneIdIndexEntry>(_entries ?? new List<SceneIdIndexEntry>());
            for (int _i = 0; _i < _incoming.Count; _i++)
            {
                SceneIdIndexEntry _entry = _incoming[_i];
                _entry.SceneGuid = _sceneGuid;
                _incoming[_i] = _entry;
            }
            _incoming.Sort(Compare);

            bool _staleChanged = staleScenes.Remove(_sceneGuid);
            if (SameEntries(_sceneGuid, _incoming))
            {
                if (_staleChanged) Save();
                return;
            }

            entries.RemoveAll(_e => _e.SceneGuid == _sceneGuid);
            entries.AddRange(_incoming);
            entries.Sort(Compare);
            Save();
        }

        /// <summary>Removes every entry of a scene and its stale mark.</summary>
        public void RemoveScene(string _sceneGuid)
        {
            int _removed = entries.RemoveAll(_e => _e.SceneGuid == _sceneGuid);
            bool _staleChanged = staleScenes.Remove(_sceneGuid);
            if (_removed > 0 || _staleChanged) Save();
        }

        public void SetPrefabsWithIds(List<string> _paths)
        {
            List<string> _sorted = new List<string>(_paths ?? new List<string>());
            _sorted.Sort(StringComparer.Ordinal);
            if (SameStrings(prefabsWithIds, _sorted)) return;

            prefabsWithIds = _sorted;
            Save();
        }

        /// <summary>Marks a scene to be read again. Kept in memory only: it survives domain reloads but is not written to disk on its own.</summary>
        public void MarkStale(string _sceneGuid)
        {
            if (string.IsNullOrEmpty(_sceneGuid) || staleScenes.Contains(_sceneGuid)) return;
            staleScenes.Add(_sceneGuid);
            staleScenes.Sort(StringComparer.Ordinal);
        }

        /// <summary>Scene guids that have entries in the index.</summary>
        internal List<string> SceneGuids()
        {
            List<string> _result = new List<string>();
            for (int _i = 0; _i < entries.Count; _i++)
                if (_result.Count == 0 || _result[_result.Count - 1] != entries[_i].SceneGuid) _result.Add(entries[_i].SceneGuid);
            return _result;
        }

        /// <summary>Defers writing the asset until the matching <see cref="EndBatch"/>.</summary>
        internal void BeginBatch() => batchDepth++;

        internal void EndBatch()
        {
            if (batchDepth > 0) batchDepth--;
            if (batchDepth > 0 || !saveDeferred) return;
            saveDeferred = false;
            Save();
        }

        // ==================== SERIALIZATION ====================

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            for (int _i = 0; _i < entries.Count; _i++)
            {
                SceneIdIndexEntry _entry = entries[_i];
                _entry.typesUnknown = _entry.ComponentTypes == null;
                entries[_i] = _entry;
            }
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            for (int _i = 0; _i < entries.Count; _i++)
            {
                SceneIdIndexEntry _entry = entries[_i];
                if (_entry.typesUnknown) _entry.ComponentTypes = null;
                entries[_i] = _entry;
            }
        }

        // ==================== PRIVATE ====================

        private static SceneIdIndex CreateProjectIndex()
        {
            if (!AssetDatabase.IsValidFolder("Assets/HarryTools")) AssetDatabase.CreateFolder("Assets", "HarryTools");
            if (!AssetDatabase.IsValidFolder(DefaultFolder)) AssetDatabase.CreateFolder("Assets/HarryTools", "SceneRefs");

            cached = CreateIndexAt(DefaultPath);
            return cached;
        }

        /// <summary>
        /// Creates an empty index asset at the path (the folder must exist). When a file is already there it could not be loaded
        /// (git conflict markers or a broken file): it is replaced and a warning asks for a Rebuild.
        /// </summary>
        internal static SceneIdIndex CreateIndexAt(string _path)
        {
            bool _unloadable = File.Exists(_path);

            SceneIdIndex _index = CreateInstance<SceneIdIndex>();
            AssetDatabase.CreateAsset(_index, _path);
            AssetDatabase.SaveAssetIfDirty(_index);

            if (_unloadable)
                Logger.LogWarning(Category, $"The scene id index '{_path}' could not be loaded (a git merge conflict or a broken file) and was recreated empty. Run Tools > HarryTools > SceneRefs > Rebuild Id Index.");
            return _index;
        }

        private void Save()
        {
            if (batchDepth > 0)
            {
                saveDeferred = true;
                return;
            }

            if (!EditorUtility.IsPersistent(this)) return;
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssetIfDirty(this);
        }

        private static int Compare(SceneIdIndexEntry _a, SceneIdIndexEntry _b)
        {
            int _scene = string.CompareOrdinal(_a.SceneGuid, _b.SceneGuid);
            return _scene != 0 ? _scene : string.CompareOrdinal(_a.Guid, _b.Guid);
        }

        private bool SameEntries(string _sceneGuid, List<SceneIdIndexEntry> _sorted)
        {
            int _start = entries.FindIndex(_e => _e.SceneGuid == _sceneGuid);
            int _count = 0;
            if (_start >= 0)
                while (_start + _count < entries.Count && entries[_start + _count].SceneGuid == _sceneGuid) _count++;
            if (_count != _sorted.Count) return false;

            for (int _i = 0; _i < _count; _i++)
                if (!SameEntry(entries[_start + _i], _sorted[_i])) return false;
            return true;
        }

        private static bool SameEntry(SceneIdIndexEntry _a, SceneIdIndexEntry _b)
        {
            if (_a.Guid != _b.Guid || _a.Id != _b.Id || _a.SceneName != _b.SceneName || _a.HierarchyPath != _b.HierarchyPath) return false;
            if (_a.ComponentTypes == null || _b.ComponentTypes == null) return _a.ComponentTypes == _b.ComponentTypes;
            return SameStrings(_a.ComponentTypes, _b.ComponentTypes);
        }

        private static bool SameStrings(IReadOnlyList<string> _a, IReadOnlyList<string> _b)
        {
            if (_a.Count != _b.Count) return false;
            for (int _i = 0; _i < _a.Count; _i++)
                if (_a[_i] != _b[_i]) return false;
            return true;
        }
    }
}
