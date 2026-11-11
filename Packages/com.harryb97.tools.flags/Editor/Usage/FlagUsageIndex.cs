using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HarryTools.Flags.Editor
{
    /// <summary>
    /// Editor-only cache of every FlagRef in the project's scenes and assets. Everything in it can be rebuilt with
    /// Rebuild Usage Index. Entries are kept sorted by scene or asset, path and flag so diffs stay small.
    /// </summary>
    internal sealed class FlagUsageIndex : ScriptableObject
    {
        // ==================== VARIABLES ====================

        [SerializeField] private List<FlagUsageEntry> entries = new List<FlagUsageEntry>();

        [NonSerialized] private int batchDepth;
        [NonSerialized] private bool saveDeferred;

        // ==================== PROPERTIES ====================

        public IReadOnlyList<FlagUsageEntry> Entries => entries;

        // ==================== PUBLIC API ====================

        /// <summary>Every use of one flag.</summary>
        public IEnumerable<FlagUsageEntry> For(string _flagGuid)
        {
            if (string.IsNullOrEmpty(_flagGuid)) yield break;
            for (int _i = 0; _i < entries.Count; _i++)
                if (entries[_i].flagGuid == _flagGuid) yield return entries[_i];
        }

        /// <summary>Replaces every entry of a scene.</summary>
        internal void ReplaceScene(string _sceneGuid, List<FlagUsageEntry> _entries)
        {
            if (string.IsNullOrEmpty(_sceneGuid)) return;

            List<FlagUsageEntry> _incoming = Prepare(_entries, _sceneGuid, string.Empty);
            Replace(_entry => _entry.sceneGuid == _sceneGuid, _incoming);
        }

        /// <summary>Replaces every entry of an asset.</summary>
        internal void ReplaceAsset(string _assetPath, List<FlagUsageEntry> _entries)
        {
            if (string.IsNullOrEmpty(_assetPath)) return;

            List<FlagUsageEntry> _incoming = Prepare(_entries, string.Empty, _assetPath);
            Replace(_entry => _entry.assetPath == _assetPath, _incoming);
        }

        internal void RemoveScene(string _sceneGuid)
        {
            if (string.IsNullOrEmpty(_sceneGuid)) return;
            if (entries.RemoveAll(_entry => _entry.sceneGuid == _sceneGuid) > 0) Save();
        }

        internal void RemoveAsset(string _assetPath)
        {
            if (string.IsNullOrEmpty(_assetPath)) return;
            if (entries.RemoveAll(_entry => _entry.assetPath == _assetPath) > 0) Save();
        }

        /// <summary>Scene guids that have entries.</summary>
        internal List<string> SceneGuids() => Keys(_entry => _entry.sceneGuid);

        /// <summary>Asset paths that have entries.</summary>
        internal List<string> AssetPaths() => Keys(_entry => _entry.assetPath);

        /// <summary>Defers writing the asset until the matching <see cref="EndBatch"/>.</summary>
        internal void BeginBatch() => batchDepth++;

        internal void EndBatch()
        {
            if (batchDepth > 0) batchDepth--;
            if (batchDepth > 0 || !saveDeferred) return;
            saveDeferred = false;
            Save();
        }

        // ==================== PRIVATE ====================

        private static List<FlagUsageEntry> Prepare(List<FlagUsageEntry> _entries, string _sceneGuid, string _assetPath)
        {
            List<FlagUsageEntry> _incoming = new List<FlagUsageEntry>();
            if (_entries != null)
            {
                foreach (FlagUsageEntry _entry in _entries)
                {
                    if (_entry == null || string.IsNullOrEmpty(_entry.flagGuid)) continue;
                    _entry.sceneGuid = _sceneGuid;
                    _entry.assetPath = _assetPath;
                    _incoming.Add(_entry);
                }
            }
            _incoming.Sort(Compare);
            return _incoming;
        }

        private void Replace(Predicate<FlagUsageEntry> _ofKey, List<FlagUsageEntry> _sorted)
        {
            List<FlagUsageEntry> _current = entries.FindAll(_ofKey);
            if (Same(_current, _sorted)) return;

            entries.RemoveAll(_ofKey);
            entries.AddRange(_sorted);
            entries.Sort(Compare);
            Save();
        }

        private List<string> Keys(Func<FlagUsageEntry, string> _key)
        {
            List<string> _result = new List<string>();
            HashSet<string> _seen = new HashSet<string>(StringComparer.Ordinal);
            for (int _i = 0; _i < entries.Count; _i++)
            {
                string _value = _key(entries[_i]);
                if (!string.IsNullOrEmpty(_value) && _seen.Add(_value)) _result.Add(_value);
            }
            return _result;
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

        private static bool Same(List<FlagUsageEntry> _a, List<FlagUsageEntry> _b)
        {
            // Both lists are sorted with the same order.
            if (_a.Count != _b.Count) return false;
            for (int _i = 0; _i < _a.Count; _i++)
                if (!_a[_i].SameAs(_b[_i])) return false;
            return true;
        }

        private static int Compare(FlagUsageEntry _a, FlagUsageEntry _b)
        {
            int _result = string.CompareOrdinal(_a.sceneGuid, _b.sceneGuid);
            if (_result == 0) _result = string.CompareOrdinal(_a.assetPath, _b.assetPath);
            if (_result == 0) _result = string.CompareOrdinal(_a.hierarchyPath, _b.hierarchyPath);
            if (_result == 0) _result = string.CompareOrdinal(_a.componentType, _b.componentType);
            if (_result == 0) _result = string.CompareOrdinal(_a.flagGuid, _b.flagGuid);
            if (_result == 0) _result = _a.kind.CompareTo(_b.kind);
            if (_result == 0) _result = _a.approximate.CompareTo(_b.approximate);
            return _result;
        }
    }
}
