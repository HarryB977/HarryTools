using UnityEditor;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Drops the runtime state of the dialogues when Play ends. Without a domain reload the runs of the last session would stay, and the main slot would stay taken </summary>
    [InitializeOnLoad]
    internal static class DialoguePlayModeReset {
        // ==================== CONSTRUCTION ====================
        #region Construction
        static DialoguePlayModeReset() {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }
        #endregion

        // ==================== METHODS ====================
        #region Methods
        internal static void OnPlayModeStateChanged(PlayModeStateChange _change) {
            if (_change == PlayModeStateChange.EnteredEditMode) Dialogue.Reset();
        }
        #endregion
    }
}
