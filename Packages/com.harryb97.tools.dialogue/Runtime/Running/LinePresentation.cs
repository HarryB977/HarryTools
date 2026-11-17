using UnityEngine;

namespace HarryTools.Dialogue {
    /// <summary> Everything a line channel needs to show one line </summary>
    public sealed class LinePresentation {
        // ==================== VARIABLES ===================
        #region Public Variables
        public DialogueRun Run { get; }
        public LineNode Line { get; }

        /// <summary> The actor speaking the line, or null when nobody does </summary>
        public DialogueActor Actor { get; internal set; }

        /// <summary> The scene object of <see cref="Actor"/>, or null </summary>
        public GameObject ActorObject => Actor != null ? Actor.gameObject : null;

        /// <summary> How long the line stays once typed, with the line override or the project default already applied </summary>
        public LineLifetime Lifetime { get; }

        /// <summary> How the line advances, with the line override or the project default already applied </summary>
        public AdvanceMode Advance { get; }

        /// <summary> Clock of the run </summary>
        public DialogueTime Time { get; }
        #endregion

        // ==================== START ====================
        internal LinePresentation(DialogueRun _run, LineNode _line, DialogueActor _actor, LineLifetime _lifetime, AdvanceMode _advance, DialogueTime _time) {
            Run = _run;
            Line = _line;
            Actor = _actor;
            Lifetime = _lifetime;
            Advance = _advance;
            Time = _time;
        }
    }
}
