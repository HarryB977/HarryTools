using System;
using System.Collections.Generic;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HarryTools.Flags.Editor
{
    /// <summary>
    /// Fails a build while the flag database has duplicate guids or names, and warns about broken references.
    /// Does nothing when the project has no flag database.
    /// </summary>
    internal sealed class FlagsBuildGuard : IPreprocessBuildWithReport
    {
        // ==================== PROPERTIES ====================

        public int callbackOrder => 0;

        // ==================== CALLBACK ====================

        public void OnPreprocessBuild(BuildReport _report)
        {
            FlagDatabase _database = FlagDatabaseLocator.Find();
            if (_database == null) return;

            List<FlagIssue> _issues;
            try
            {
                _issues = FlagValidator.Validate(_database, FlagUsageIndexLocator.Find());
            }
            catch (Exception _exception)
            {
                Debug.LogError($"Build blocked: the flag validation failed ({_exception.Message}).\n{_exception}");
                throw new BuildFailedException($"Build blocked: the flag validation failed ({_exception.Message}). See console.");
            }

            int _blocking = 0;
            foreach (FlagIssue _issue in _issues)
            {
                FlagValidator.Log(_issue);
                if (_issue.Blocking) _blocking++;
            }

            if (_blocking > 0) throw new BuildFailedException($"Build blocked: {_blocking} flag database conflict(s). See console.");
        }
    }
}
