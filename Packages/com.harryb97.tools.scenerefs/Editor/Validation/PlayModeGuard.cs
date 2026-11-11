using System;
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

        /// <summary>True when Play must be blocked. Returns the conflicts found. A validation that fails is logged and blocks.</summary>
        internal static bool ShouldBlock(out List<SceneIdConflict> _conflicts)
        {
            try
            {
                _conflicts = SceneIdValidator.ValidateProjectForGuard();
                return _conflicts.Count > 0;
            }
            catch (Exception _exception)
            {
                _conflicts = new List<SceneIdConflict>();
                Debug.LogError($"Play blocked: the scene id validation failed ({_exception.Message}). Fix the error or run Tools > HarryTools > SceneRefs > Rebuild Id Index.\n{_exception}");
                return true;
            }
        }

        // ==================== PRIVATE ====================

        private static void OnPlayModeStateChanged(PlayModeStateChange _state)
        {
            if (_state != PlayModeStateChange.ExitingEditMode) return;
            if (!ShouldBlock(out List<SceneIdConflict> _conflicts)) return;

            EditorApplication.isPlaying = false;
            // A failed validation was already logged by ShouldBlock.
            if (_conflicts.Count == 0) return;
            for (int _i = 0; _i < _conflicts.Count; _i++)
                Debug.LogError(_conflicts[_i].ToReport(), _conflicts[_i].FindContext());
            Debug.LogError($"Play blocked: {_conflicts.Count} scene id conflict(s).");
        }
    }
}
