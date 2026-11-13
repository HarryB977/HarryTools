using System;
using System.Collections.Generic;
using UnityEditor;

namespace HarryTools.Flags.Editor
{
    /// <summary>Soft warnings about one flag, shown in the Flags window. None of them blocks anything.</summary>
    internal static class FlagWarnings
    {
        // ==================== PUBLIC API ====================

        /// <summary>Warning texts of the flag. Usage warnings are skipped when there is no index.</summary>
        public static IReadOnlyList<string> For(FlagDefinition _flag, FlagDatabase _database, FlagUsageIndex _index)
        {
            List<string> _warnings = new List<string>();
            if (_flag == null) return _warnings;

            if (_index != null) AddUsageWarnings(_flag, _index, _warnings);
            if (_database != null) AddSameNameWarning(_flag, _database, _warnings);

            if (_flag.Scope == FlagScope.Local && string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(_flag.SceneGuid ?? string.Empty)))
                _warnings.Add("Scene is missing");
            return _warnings;
        }

        // ==================== PRIVATE ====================

        private static void AddUsageWarnings(FlagDefinition _flag, FlagUsageIndex _index, List<string> _warnings)
        {
            int _reads = 0, _writes = 0, _others = 0;
            foreach (FlagUsageEntry _usage in _index.For(_flag.Guid))
            {
                if (_usage.kind == FlagUsageKind.Read) _reads++;
                else if (_usage.kind == FlagUsageKind.Write) _writes++;
                else _others++;
            }

            if (_reads + _writes + _others == 0) _warnings.Add("Not used anywhere");
            // An "Other" usage may read or write, so it silences the one-sided warnings.
            else if (_others > 0) return;
            else if (_writes == 0) _warnings.Add("Read but never set");
            else if (_reads == 0) _warnings.Add("Set but never read");
        }

        private static void AddSameNameWarning(FlagDefinition _flag, FlagDatabase _database, List<string> _warnings)
        {
            foreach (FlagDefinition _other in _database.Flags)
            {
                if (_other == null || _other.Scope == _flag.Scope || !string.Equals(_other.Name, _flag.Name, StringComparison.OrdinalIgnoreCase)) continue;

                if (_flag.Scope == FlagScope.Local)
                {
                    _warnings.Add("A global flag with the same name exists");
                    return;
                }
                _warnings.Add("A local flag with the same name exists in " + _other.SceneName);
                return;
            }
        }
    }
}
