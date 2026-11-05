using System;
using UnityEngine;
using UnityEngine.Localization;

namespace HarryTools.Audio.Subtitles {
    /// <summary> One subtitle line of a sound: what to show, when it starts and how long it lasts. </summary>
    [Serializable]
    public sealed class SubtitleEntry {
        // ==================== VARIABLES ===================
        [Tooltip("Localized text, delivered exactly as it comes out of the table.")]
        public LocalizedString Text;
        [Tooltip("Dialogue is spoken text. Caption describes a sound.")]
        public SubtitleKind Kind;
        [Min(0), Tooltip("Seconds after the sound starts before this line appears.")]
        public float Delay;
        [Min(0), Tooltip("Seconds the line stays visible. 0 lasts until the next entry of the same kind or the end of the audio.")]
        public float Duration;
    }
}
