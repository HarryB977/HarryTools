using System;
using System.Collections.Generic;

namespace HarryTools.SceneRefs.Editor
{
    /// <summary>
    /// Finds the scene id conflicts that block Play and builds.
    /// </summary>
    internal static class SceneIdValidator
    {
        // ==================== PUBLIC API ====================

        /// <summary>Pure check over a set of entries and the prefab paths that contain ids.</summary>
        public static List<SceneIdConflict> Validate(IEnumerable<SceneIdIndexEntry> _entries, IReadOnlyList<string> _prefabsWithIds)
        {
            List<SceneIdConflict> _conflicts = new List<SceneIdConflict>();
            List<SceneIdIndexEntry> _all = new List<SceneIdIndexEntry>(_entries ?? new List<SceneIdIndexEntry>());

            Dictionary<string, SceneIdConflict> _byGuid = new Dictionary<string, SceneIdConflict>(StringComparer.Ordinal);
            Dictionary<string, SceneIdConflict> _byId = new Dictionary<string, SceneIdConflict>(SceneIdNaming.Comparer);
            List<SceneIdConflict> _guidConflicts = new List<SceneIdConflict>();
            List<SceneIdConflict> _idConflicts = new List<SceneIdConflict>();

            for (int _i = 0; _i < _all.Count; _i++)
            {
                SceneIdIndexEntry _entry = _all[_i];
                if (!string.IsNullOrEmpty(_entry.Guid)) Collect(_byGuid, _guidConflicts, SceneIdConflictKind.DuplicateGuid, _entry.Guid, _entry);
                if (!string.IsNullOrEmpty(_entry.Id)) Collect(_byId, _idConflicts, SceneIdConflictKind.DuplicateId, _entry.Id, _entry);
            }

            AddRepeated(_conflicts, _guidConflicts);
            AddRepeated(_conflicts, _idConflicts);

            if (_prefabsWithIds != null)
                for (int _i = 0; _i < _prefabsWithIds.Count; _i++)
                    _conflicts.Add(new SceneIdConflict { Kind = SceneIdConflictKind.IdInPrefabAsset, Key = _prefabsWithIds[_i], PrefabPath = _prefabsWithIds[_i] });

            return _conflicts;
        }

        /// <summary>
        /// Validates the whole project: stale scenes are read again, scenes that are not loaded come from the index
        /// and loaded scenes (saved or not) are read from memory.
        /// </summary>
        public static List<SceneIdConflict> ValidateProject()
        {
            SceneIdIndexMaintainer.RefreshStale(true);

            // Never creates the project index just to validate.
            SceneIdIndex _index = SceneIdIndex.Existing;
            return Validate(SceneIndexer.ProjectEntries(_index), _index != null ? _index.PrefabsWithIds : null);
        }

        // ==================== PRIVATE ====================

        private static void Collect(Dictionary<string, SceneIdConflict> _map, List<SceneIdConflict> _order, SceneIdConflictKind _kind, string _key, SceneIdIndexEntry _entry)
        {
            if (!_map.TryGetValue(_key, out SceneIdConflict _conflict))
            {
                _conflict = new SceneIdConflict { Kind = _kind, Key = _key };
                _map.Add(_key, _conflict);
                _order.Add(_conflict);
            }

            _conflict.Entries.Add(_entry);
        }

        private static void AddRepeated(List<SceneIdConflict> _target, List<SceneIdConflict> _candidates)
        {
            for (int _i = 0; _i < _candidates.Count; _i++)
                if (_candidates[_i].Entries.Count > 1) _target.Add(_candidates[_i]);
        }
    }
}
