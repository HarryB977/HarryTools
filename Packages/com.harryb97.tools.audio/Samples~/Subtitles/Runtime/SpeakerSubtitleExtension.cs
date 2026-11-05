using System;
using UnityEngine;
using UnityEngine.Localization;

namespace HarryTools.Audio.Subtitles {
    /// <summary> Adds the localized display name of a speaker, shown before its dialogue lines. </summary>
    [Serializable]
    public sealed class SpeakerSubtitleExtension : IAudioAssetExtension {
        // ==================== VARIABLES ===================
        [Tooltip("Localized name shown for this speaker.")]
        public LocalizedString Name;
    }
}
