using UnityEngine;
using UnityEngine.Audio;

namespace HarryTools.Audio {
    /// <summary>
    /// Filters and output applied to everything an AudioEmitter says or plays (or to every line of a speaker through its DefaultEffect).
    /// A phone call, a radio or a helmet. Not applied on platforms without mixer (WebGL).
    /// </summary>
    [CreateAssetMenu(menuName = "HarryTools/Audio/Effect Preset", fileName = "Effect Preset")]
    public sealed class AudioEffectPreset : ScriptableObject {
        // ==================== VARIABLES ===================
        [Tooltip("Optional mixer group. Wins over the OutputOverride of the asset and over its bus group.")]
        public AudioMixerGroup Output;

        [Header("Low pass")]
        public bool LowPass;
        [Tooltip("Cutoff frequency in Hz.")]
        public float LowPassCutoff = 5000f;
        [Tooltip("Resonance (Q) of the low-pass filter.")]
        public float LowPassResonance = 1f;

        [Header("High pass")]
        public bool HighPass;
        [Tooltip("Cutoff frequency in Hz.")]
        public float HighPassCutoff = 300f;

        [Header("Distortion")]
        public bool Distortion;
        [Range(0, 1)] public float DistortionLevel = 0.5f;

        [Header("Echo")]
        public bool Echo;
        [Tooltip("Echo delay in milliseconds.")]
        public float EchoDelay = 300f;
        [Range(0, 1), Tooltip("Volume of each echo relative to the previous one.")]
        public float EchoDecay = 0.5f;
        [Range(0, 1), Tooltip("Volume of the echoed signal.")]
        public float EchoWet = 1f;

        // ==================== UNITY ===================
        private void OnValidate() {
            LowPassCutoff = Mathf.Clamp(LowPassCutoff, 10f, 22000f);
            LowPassResonance = Mathf.Clamp(LowPassResonance, 1f, 10f);
            HighPassCutoff = Mathf.Clamp(HighPassCutoff, 10f, 22000f);
            EchoDelay = Mathf.Clamp(EchoDelay, 10f, 5000f);
        }
    }
}
