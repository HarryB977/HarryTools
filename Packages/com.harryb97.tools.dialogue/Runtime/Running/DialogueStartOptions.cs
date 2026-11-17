using UnityEngine;

namespace HarryTools.Dialogue {
    /// <summary> How <see cref="Dialogue.Start"/> runs a dialogue. Unset values fall back to the dialogue asset, then to <see cref="DialogueSettings"/> </summary>
    public sealed class DialogueStartOptions {
        // ==================== VARIABLES ===================
        #region Public Variables
        /// <summary> Main dialogues can offer choices and only one runs at a time. Ambient ones have no limit </summary>
        public bool IsMain { get; set; } = true;

        /// <summary> What this dialogue does, when it runs as ambient, as a main dialogue starts </summary>
        public AmbientPolicy? AmbientPolicy { get; set; }

        /// <summary> Clock of this run </summary>
        public DialogueTime? Time { get; set; }

        /// <summary> The object that started the dialogue. Target of every action and condition the dialogue runs </summary>
        public GameObject Origin { get; set; }
        #endregion
    }
}
