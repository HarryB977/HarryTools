using UnityEngine;

namespace HarryTools.Audio {
    /// <summary> What the acoustic modules want for one voice. The default values of <see cref="Neutral"/> change nothing. </summary>
    public struct AcousticResult {
        // ==================== VARIABLES ===================
        public const float NeutralLowPass = 22000f;
        public const float NeutralHighPass = 10f;

        /// <summary> Linear multiplier on the voice volume. 1 is neutral. </summary>
        public float VolumeMultiplier;
        /// <summary> Low-pass cutoff in Hz. 22000 is neutral. </summary>
        public float LowPassCutoff;
        /// <summary> High-pass cutoff in Hz. 10 is neutral. </summary>
        public float HighPassCutoff;
        /// <summary> When true the AudioSource is heard from PerceivedPosition instead of its logical position (for example a doorway). </summary>
        public bool HasPerceivedPosition;
        public Vector3 PerceivedPosition;

        // ==================== PROPERTIES ===================
        public static AcousticResult Neutral => new AcousticResult {
            VolumeMultiplier = 1f,
            LowPassCutoff = NeutralLowPass,
            HighPassCutoff = NeutralHighPass,
        };
    }
}
