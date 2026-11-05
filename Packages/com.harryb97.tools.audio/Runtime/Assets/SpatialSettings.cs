using System;
using UnityEngine;

namespace HarryTools.Audio {
    /// <summary> 3D sound parameters applied to an AudioSource. </summary>
    [Serializable]
    public struct SpatialSettings {
        // ==================== VARIABLES ===================
        [Range(0, 1), Tooltip("0 = fully 2D, 1 = fully 3D.")]
        public float SpatialBlend;
        [Tooltip("Volume attenuation over distance.")]
        public AudioRolloffMode Rolloff;
        [Tooltip("Only used when Rolloff is Custom.")]
        public AnimationCurve CustomRolloff;
        [Min(0), Tooltip("Distance below which the sound plays at full volume.")]
        public float MinDistance;
        [Min(0), Tooltip("Distance beyond which the sound stops attenuating.")]
        public float MaxDistance;
        [Range(0, 360), Tooltip("Stereo spread angle in degrees.")]
        public float Spread;
        [Range(0, 5), Tooltip("Doppler effect intensity.")]
        public float Doppler;

        // ==================== PRESETS ===================
        public static SpatialSettings Default2D => new SpatialSettings {
            SpatialBlend = 0f,
            Rolloff = AudioRolloffMode.Logarithmic,
            MinDistance = 1f,
            MaxDistance = 50f,
            Spread = 0f,
            Doppler = 1f
        };

        public static SpatialSettings Default3D => new SpatialSettings {
            SpatialBlend = 1f,
            Rolloff = AudioRolloffMode.Logarithmic,
            MinDistance = 1f,
            MaxDistance = 50f,
            Spread = 0f,
            Doppler = 1f
        };

        // ==================== PROPERTIES ===================
        /// <summary> True when CustomRolloff has at least one key. </summary>
        public bool HasCustomCurve => CustomRolloff != null && CustomRolloff.length > 0;

        // ==================== PUBLIC ===================
        public void ApplyTo(AudioSource _source) {
            _source.spatialBlend = SpatialBlend;
            // A custom rolloff without keys would silence the source: fall back to logarithmic.
            if (Rolloff == AudioRolloffMode.Custom && HasCustomCurve) {
                _source.rolloffMode = AudioRolloffMode.Custom;
                _source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, CustomRolloff);
            } else {
                _source.rolloffMode = Rolloff == AudioRolloffMode.Custom ? AudioRolloffMode.Logarithmic : Rolloff;
            }
            _source.minDistance = MinDistance;
            _source.maxDistance = MaxDistance;
            _source.spread = Spread;
            _source.dopplerLevel = Doppler;
        }
    }
}
