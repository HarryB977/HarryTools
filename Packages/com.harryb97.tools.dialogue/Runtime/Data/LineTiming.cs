using System;
using UnityEngine;

namespace HarryTools.Dialogue {
    public enum LineLifetimeMode {
        [Tooltip("A fixed number of seconds")] Fixed,
        [Tooltip("A base time plus a time per character")] ByTextLength,
        [Tooltip("The length of the typing voice. Behaves as By Text Length for now")] ByVoice,
        [Tooltip("The line stays until the next one replaces it")] UntilNext
    }

    public enum AdvanceMode {
        [Tooltip("The player advances with input")] Input,
        [Tooltip("The line advances on its own when its lifetime ends")] Auto,
        [Tooltip("Both input and the lifetime advance the line")] Both
    }

    public enum LineExit {
        [Tooltip("A line leaves when a new line needs its place or its dialogue ends")] PushedOut,
        [Tooltip("A line leaves once its stay time is over, even after its dialogue ends. Max Visible does not apply")] AfterTime,
        [Tooltip("A line leaves when a new line needs its place or once its stay time is over, even after its dialogue ends")] Both
    }

    public enum DialogueTime { Scaled, Unscaled }

    public enum AmbientPolicy {
        [Tooltip("Ambient dialogues keep running while a main one plays")] Continue,
        [Tooltip("Ambient dialogues pause while a main one plays")] Pause,
        [Tooltip("Ambient dialogues are stopped while a main one plays")] Silence
    }

    /// <summary> How long a line stays on screen after it has been typed </summary>
    [Serializable]
    public struct LineLifetime {
        // ==================== VARIABLES ===================
        #region Public Variables
        public LineLifetimeMode Mode;
        [Min(0f)] public float Seconds;
        [Min(0f)] public float Base;
        [Min(0f)] public float PerCharacter;

        /// <summary> By text length with the package defaults. Structs cannot use field initializers, so defaults live here </summary>
        public static LineLifetime Default => new LineLifetime {
            Mode = LineLifetimeMode.ByTextLength,
            Seconds = 3f,
            Base = 1.5f,
            PerCharacter = 0.05f
        };
        #endregion

        // ==================== METHODS ====================
        /// <summary> Seconds the line stays for a text of the given length. Infinity when it waits for the next line </summary>
        public float Resolve(int _characters) {
            switch (Mode) {
                case LineLifetimeMode.Fixed: return Seconds;
                case LineLifetimeMode.UntilNext: return float.PositiveInfinity;
                default: return Base + PerCharacter * Mathf.Max(0, _characters);
            }
        }
    }
}
