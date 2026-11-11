using System;
using System.Collections.Generic;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HarryTools.SceneRefs.Editor
{
    /// <summary>
    /// Fails a build while the project has scene id conflicts, in any scene of the project, or when the validation itself fails.
    /// </summary>
    internal sealed class BuildGuard : IPreprocessBuildWithReport
    {
        // ==================== PROPERTIES ====================

        public int callbackOrder => 0;

        // ==================== CALLBACK ====================

        public void OnPreprocessBuild(BuildReport _report)
        {
            List<SceneIdConflict> _conflicts;
            try
            {
                _conflicts = SceneIdValidator.ValidateProjectForGuard();
            }
            catch (Exception _exception)
            {
                Debug.LogError($"Build blocked: the scene id validation failed ({_exception.Message}).\n{_exception}");
                throw new BuildFailedException($"Build blocked: the scene id validation failed ({_exception.Message}). See console.");
            }

            if (_conflicts.Count == 0) return;

            for (int _i = 0; _i < _conflicts.Count; _i++)
                Debug.LogError(_conflicts[_i].ToReport(), _conflicts[_i].FindContext());
            throw new BuildFailedException($"Build blocked: {_conflicts.Count} scene id conflict(s). See console.");
        }
    }
}
