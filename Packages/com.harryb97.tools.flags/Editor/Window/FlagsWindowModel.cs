using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace HarryTools.Flags.Editor
{
    /// <summary>Which flags the Flags window lists.</summary>
    internal enum FlagFilter
    {
        All,
        Global,
        Local
    }

    /// <summary>One group of the Flags window list: the globals, the locals of one scene, or the locals whose scene is gone.</summary>
    internal sealed class FlagGroup
    {
        public string Title;

        /// <summary>Guid of the scene of a local group. Empty for the Global and Missing scene groups.</summary>
        public string SceneGuid;

        public bool SceneLoaded;

        public bool SceneMissing;

        public List<FlagDefinition> Flags = new List<FlagDefinition>();
    }

    /// <summary>Data behind the Flags window list, kept apart from the IMGUI so it can be tested.</summary>
    internal static class FlagsWindowModel
    {
        // ==================== VARIABLES ====================

        internal const string GlobalTitle = "Global";
        internal const string MissingSceneTitle = "Missing scene";

        // ==================== METHODS ====================

        /// <summary>
        /// Groups the flags that match the search (by name, ignoring case) and the filter: Global first, then one group per scene ordered
        /// by name, then Missing scene. Groups without flags are left out. Flags are ordered by name inside each group.
        /// </summary>
        public static List<FlagGroup> Build(FlagDatabase _database, string _search, FlagFilter _filter)
        {
            List<FlagGroup> _result = new List<FlagGroup>();
            if (_database == null) return _result;

            FlagGroup _global = new FlagGroup { Title = GlobalTitle, SceneGuid = string.Empty };
            FlagGroup _missing = new FlagGroup { Title = MissingSceneTitle, SceneGuid = string.Empty, SceneMissing = true };
            Dictionary<string, FlagGroup> _scenes = new Dictionary<string, FlagGroup>(StringComparer.Ordinal);

            foreach (FlagDefinition _flag in _database.Flags)
            {
                if (_flag == null || !Matches(_flag, _search, _filter)) continue;

                if (_flag.Scope == FlagScope.Global)
                {
                    _global.Flags.Add(_flag);
                    continue;
                }

                string _path = AssetDatabase.GUIDToAssetPath(_flag.SceneGuid ?? string.Empty);
                if (string.IsNullOrEmpty(_path))
                {
                    _missing.Flags.Add(_flag);
                    continue;
                }

                if (!_scenes.TryGetValue(_flag.SceneGuid, out FlagGroup _scene))
                {
                    _scene = new FlagGroup
                    {
                        Title = string.IsNullOrEmpty(_flag.SceneName) ? System.IO.Path.GetFileNameWithoutExtension(_path) : _flag.SceneName,
                        SceneGuid = _flag.SceneGuid,
                        SceneLoaded = SceneManager.GetSceneByPath(_path).isLoaded
                    };
                    _scenes.Add(_flag.SceneGuid, _scene);
                }
                _scene.Flags.Add(_flag);
            }

            if (_global.Flags.Count > 0) _result.Add(_global);
            _result.AddRange(_scenes.Values
                .OrderBy(_group => _group.Title, StringComparer.OrdinalIgnoreCase)
                .ThenBy(_group => _group.SceneGuid, StringComparer.Ordinal));
            if (_missing.Flags.Count > 0) _result.Add(_missing);

            foreach (FlagGroup _group in _result) _group.Flags.Sort(CompareFlags);
            return _result;
        }

        /// <summary>How many FlagRefs of the project point at the flag.</summary>
        public static int UsageCount(FlagUsageIndex _index, string _guid) => _index == null ? 0 : _index.For(_guid).Count();

        // ==================== PRIVATE ====================

        private static bool Matches(FlagDefinition _flag, string _search, FlagFilter _filter)
        {
            if (_filter == FlagFilter.Global && _flag.Scope != FlagScope.Global) return false;
            if (_filter == FlagFilter.Local && _flag.Scope != FlagScope.Local) return false;
            if (string.IsNullOrEmpty(_search)) return true;
            return !string.IsNullOrEmpty(_flag.Name) && _flag.Name.IndexOf(_search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // In the Missing scene group, flags of the same lost scene stay together.
        private static int CompareFlags(FlagDefinition _a, FlagDefinition _b)
        {
            int _result = _a.Scope == FlagScope.Local ? string.Compare(_a.SceneName, _b.SceneName, StringComparison.OrdinalIgnoreCase) : 0;
            return _result != 0 ? _result : string.Compare(_a.Name, _b.Name, StringComparison.OrdinalIgnoreCase);
        }
    }
}
