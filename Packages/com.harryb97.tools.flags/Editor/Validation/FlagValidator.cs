using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HarryTools.Flags.Editor
{
    /// <summary>
    /// Checks the flag database and the usage index. Duplicate guids and duplicate names in one scope block Play and builds;
    /// broken references and flags of missing scenes only warn.
    /// </summary>
    internal static class FlagValidator
    {
        // ==================== PUBLIC API ====================

        public static List<FlagIssue> Validate(FlagDatabase _database, FlagUsageIndex _index)
        {
            List<FlagIssue> _issues = new List<FlagIssue>();
            if (_database == null) return _issues;

            CheckDefinitions(_database, _issues);
            if (_index != null) CheckUsages(_database, _index, _issues);
            return _issues;
        }

        /// <summary>Writes one console message for the issue, clickable when it points at an asset.</summary>
        public static void Log(FlagIssue _issue)
        {
            UnityEngine.Object _context = null;
            if (_issue.Usage != null && !string.IsNullOrEmpty(_issue.Usage.assetPath))
                _context = AssetDatabase.LoadMainAssetAtPath(_issue.Usage.assetPath);

            if (_issue.Blocking) Debug.LogError(_issue.Message, _context);
            else Debug.LogWarning(_issue.Message, _context);
        }

        // ==================== PRIVATE ====================

        private static void CheckDefinitions(FlagDatabase _database, List<FlagIssue> _issues)
        {
            HashSet<string> _guids = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> _names = new HashSet<string>(StringComparer.Ordinal);

            foreach (FlagDefinition _flag in _database.Flags)
            {
                if (_flag == null) continue;

                if (!_guids.Add(_flag.Guid ?? string.Empty))
                    _issues.Add(new FlagIssue(FlagIssueKind.DuplicateGuid, true,
                        $"Two flags share the guid {_flag.Guid} ('{_flag.DisplayPath}' is one of them). Delete or regenerate one in the flag database.", _flag.Guid));

                string _scopeKey = _flag.Scope == FlagScope.Global ? "global" : "local:" + _flag.SceneGuid;
                string _nameKey = _scopeKey + "/" + (_flag.Name ?? string.Empty).ToLowerInvariant();
                if (!_names.Add(_nameKey))
                    _issues.Add(new FlagIssue(FlagIssueKind.DuplicateName, true,
                        $"Two flags are called '{_flag.DisplayPath}' in the same scope (names ignore case). Rename one in the flag database.", _flag.Guid));

                if (_flag.Scope == FlagScope.Local && string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(_flag.SceneGuid ?? string.Empty)))
                    _issues.Add(new FlagIssue(FlagIssueKind.MissingScene, false,
                        $"The local flag '{_flag.DisplayPath}' belongs to a scene that no longer exists.", _flag.Guid));
            }
        }

        private static void CheckUsages(FlagDatabase _database, FlagUsageIndex _index, List<FlagIssue> _issues)
        {
            foreach (FlagUsageEntry _usage in _index.Entries)
            {
                FlagRefState _state = FlagHostScope.StateOf(_database, _usage.flagGuid, _usage.sceneGuid);
                if (_state == FlagRefState.Deleted)
                {
                    _issues.Add(new FlagIssue(FlagIssueKind.BrokenRef, false, $"{Where(_usage)} references a flag that was deleted ({_usage.flagGuid}).", _usage.flagGuid, _usage));
                }
                else if (_state == FlagRefState.ForeignLocal)
                {
                    _database.TryGet(_usage.flagGuid, out FlagDefinition _flag);
                    _issues.Add(new FlagIssue(FlagIssueKind.ForeignLocalRef, false,
                        $"{Where(_usage)} references the local flag '{_flag.DisplayPath}' from another scene or an asset.", _usage.flagGuid, _usage));
                }
            }
        }

        private static string Where(FlagUsageEntry _usage)
        {
            string _place = _usage.assetPath;
            if (string.IsNullOrEmpty(_place)) _place = AssetDatabase.GUIDToAssetPath(_usage.sceneGuid);
            if (string.IsNullOrEmpty(_place)) _place = "A scene";

            string _object = string.IsNullOrEmpty(_usage.hierarchyPath) ? string.Empty : "/" + _usage.hierarchyPath;
            string _component = string.IsNullOrEmpty(_usage.componentType) ? string.Empty : " (" + _usage.componentType + ")";
            return _place + _object + _component;
        }
    }
}
