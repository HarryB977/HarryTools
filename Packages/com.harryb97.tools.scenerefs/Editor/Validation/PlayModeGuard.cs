using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HarryTools.SceneRefs.Editor
{
    /// <summary>
    /// Cancels entering Play mode while the project has scene id conflicts.
    /// </summary>
    [InitializeOnLoad]
    internal static class PlayModeGuard
    {
        // ==================== SETUP ====================

        static PlayModeGuard()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        // ==================== DECISION ====================

        /// <summary>True when Play must be blocked. Returns the conflicts found.</summary>
        internal static bool ShouldBlock(out List<SceneIdConflict> _conflicts)
        {
            _conflicts = SceneIdValidator.ValidateProject();
            return _conflicts.Count > 0;
        }

        // ==================== PRIVATE ====================

        private static void OnPlayModeStateChanged(PlayModeStateChange _state)
        {
            if (_state != PlayModeStateChange.ExitingEditMode) return;
            if (!ShouldBlock(out List<SceneIdConflict> _conflicts)) return;

            EditorApplication.isPlaying = false;
            for (int _i = 0; _i < _conflicts.Count; _i++)
                Debug.LogError(_conflicts[_i].ToReport(), _conflicts[_i].FindContext());
            Debug.LogError($"Play blocked: {_conflicts.Count} scene id conflict(s).");
        }
    }
}
