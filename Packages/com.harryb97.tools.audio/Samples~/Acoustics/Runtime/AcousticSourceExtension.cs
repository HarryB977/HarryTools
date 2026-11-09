using System;
using UnityEngine;

namespace HarryTools.Audio.Acoustics {
    /// <summary> Acoustic settings of one audio asset. </summary>
    [Serializable]
    public sealed class AcousticSourceExtension : IAudioAssetExtension {
        // ==================== VARIABLES ===================
        [Min(0)]
        [Tooltip("Radius in meters of the sphere that stands for the source when tracing to the listener.")]
        public float Radius = 0.3f;
        [Tooltip("Skip the acoustic simulation for this asset.")]
        public bool Exclude;
        [Min(0)]
        [Tooltip("Scale of the reverb send for this asset.")]
        public float ReverbSend = 1f;
    }
}
