using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HarryTools.Flags.Editor
{
    /// <summary>
    /// Cancels entering Play mode while the flag database has duplicate guids or names, and warns about broken references.
    /// Does nothing when the project has no flag database.
    /// </summary>
    [InitializeOnLoad]
    internal static class FlagsPlayModeGuard
    {
        // ==================== SETUP ====================

        static FlagsPlayModeGuard()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        // ==================== PRIVATE ====================

        private static void OnPlayModeStateChanged(PlayModeStateChange _state)
        {
            if (_state != PlayModeStateChange.ExitingEditMode) return;

            FlagDatabase _database = FlagDatabaseLocator.Find();
            if (_database == null) return;

            List<FlagIssue> _issues;
            try
            {
                FlagUsageMaintainer.RefreshLoadedScenes();
                _issues = FlagValidator.Validate(_database, FlagUsageIndexLocator.Find());
            }
            catch (Exception _exception)
            {
                EditorApplication.isPlaying = false;
                Debug.LogError($"Play blocked: the flag validation failed ({_exception.Message}).\n{_exception}");
                return;
            }

            int _blocking = 0;
            foreach (FlagIssue _issue in _issues)
            {
                FlagValidator.Log(_issue);
                if (_issue.Blocking) _blocking++;
            }

            if (_blocking == 0) return;
            EditorApplication.isPlaying = false;
            Debug.LogError($"Play blocked: {_blocking} flag database conflict(s).");
        }
    }
}
