using System;
using System.Collections.Generic;
using UnityEngine;

namespace HarryTools.Audio.Acoustics {
    /// <summary> Links a Unity physics material to an acoustic material. </summary>
    [Serializable]
    public struct PhysicsMaterialMapping {
        public PhysicsMaterial Physics;
        public AcousticMaterial Acoustic;
    }

    /// <summary> Profile to use on one platform instead of the base profile. </summary>
    [Serializable]
    public struct PlatformProfile {
        public RuntimePlatform Platform;
        public AcousticProfile Profile;
    }

    /// <summary> Quality and tuning settings of the acoustic simulation. </summary>
    [CreateAssetMenu(menuName = "HarryTools/Audio/Acoustics/Profile")]
    public sealed class AcousticProfile : ScriptableObject {
        // ==================== VARIABLES ===================
        [Header("Features")]
        [Tooltip("Muffle sounds whose line to the listener is blocked.")]
        public bool Occlusion = true;
        [Tooltip("Let part of the sound through thin or light surfaces.")]
        public bool Transmission = true;
        [Tooltip("Dull distant sounds as they travel through air.")]
        public bool AirAbsorption = true;

        [Header("Quality")]
        [Tooltip("Points on the source sphere traced to the listener, besides the center.")]
        public int OcclusionRays = 8;
        [Tooltip("Seconds between evaluations of one voice.")]
        public float UpdateInterval = 0.1f;
        [Tooltip("Maximum surfaces counted along one ray.")]
        public int MaxHitsPerRay = 8;
        [Tooltip("Rays launched per frame across all voices.")]
        public int RayBudgetPerFrame = 256;
        [Tooltip("Milliseconds of main thread time per frame. Voices left over wait for the next frame.")]
        public float MainThreadBudgetMs = 0.5f;

        [Header("Geometry")]
        [Tooltip("Layers that block sound. Triggers never count.")]
        public LayerMask Geometry = ~(1 << 2);
        [Tooltip("Material for colliders with no AcousticSurface and no mapped physics material.")]
        public AcousticMaterial DefaultMaterial;
        [Tooltip("Acoustic material per physics material, used when a collider has no AcousticSurface.")]
        public List<PhysicsMaterialMapping> PhysicsMaterials = new List<PhysicsMaterialMapping>();

        [Header("Output")]
        [Tooltip("Volume floor in dB for a fully blocked sound.")]
        public float FloorDb = -30f;
        [Tooltip("Lowest low-pass cutoff in Hz.")]
        public float MinLowPass = 1000f;
        [Tooltip("Highest high-pass cutoff in Hz.")]
        public float MaxHighPass = 500f;
        [Tooltip("High band loss per meter of air, in dB.")]
        public float AirHighDbPerMeter = 0.02f;
        [Tooltip("Mid band loss per meter of air, in dB.")]
        public float AirMidDbPerMeter = 0.004f;

        [Header("Platforms")]
        [Tooltip("Profiles that replace this one on specific platforms.")]
        public List<PlatformProfile> PlatformOverrides = new List<PlatformProfile>();

        // ==================== PUBLIC API ===================
        /// <summary> Returns the first override for <paramref name="_platform"/>, or this profile. </summary>
        public AcousticProfile Resolve(RuntimePlatform _platform) {
            if (PlatformOverrides == null) return this;
            for (int i = 0; i < PlatformOverrides.Count; i++) {
                PlatformProfile _entry = PlatformOverrides[i];
                if (_entry.Platform == _platform && _entry.Profile != null) return _entry.Profile;
            }
            return this;
        }
    }
}
