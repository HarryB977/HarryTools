using System;
using UnityEditor;

namespace HarryTools.SceneRefs.Editor
{
    /// <summary>One-shot callbacks on the next editor update.</summary>
    internal static class EditorTick
    {
        // ==================== PUBLIC API ====================

        /// <summary>
        /// Runs the action once on the next editor update. EditorApplication.delayCall is not used because it does not run
        /// while the editor is in the background, which left queued work undone during test runs.
        /// </summary>
        public static void Next(Action _action)
        {
            EditorApplication.CallbackFunction _tick = null;
            _tick = () =>
            {
                EditorApplication.update -= _tick;
                _action();
            };
            EditorApplication.update += _tick;
        }
    }
}
