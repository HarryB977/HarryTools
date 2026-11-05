using UnityEngine;

namespace HarryTools.Audio {
    /// <summary> Conversions between linear volume and decibels. </summary>
    public static class AudioMath {
        // ==================== VARIABLES ===================
        public const float MinDb = -80f;
        private const float MinLinear = 0.0001f;

        // ==================== PUBLIC ===================
        /// <summary> 20·log10 of the volume, clamped to 0..1. Zero, NaN and anything at or below 0.0001 are -80 dB so the mixer never gets -Infinity. </summary>
        public static float LinearToDb(float _linear) {
            if (!(_linear > MinLinear)) return MinDb;
            return 20f * Mathf.Log10(Mathf.Min(_linear, 1f));
        }

        public static float DbToLinear(float _db) {
            if (_db <= MinDb) return 0f;
            return Mathf.Pow(10f, _db / 20f);
        }
    }
}
