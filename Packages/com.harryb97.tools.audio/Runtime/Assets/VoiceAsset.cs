using UnityEngine;

namespace HarryTools.Audio {
    /// <summary> A spoken line. </summary>
    [CreateAssetMenu(menuName = "HarryTools/Audio/Voice", fileName = "Voice")]
    public sealed class VoiceAsset : AudioAsset {
        // ==================== VARIABLES ===================
        [Header("Voice")]
        [SerializeField] private SpeakerAsset speaker;

        // ==================== CONSTRUCTORS ===================
        public VoiceAsset() : base(AudioBus.Voice, false, 90) { }

        // ==================== PROPERTIES ===================
        public SpeakerAsset Speaker => speaker;

        // ==================== INTERNAL ===================
        internal void SetSpeaker(SpeakerAsset _speaker) => speaker = _speaker;
    }
}
