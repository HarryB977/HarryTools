using System;
using UnityEngine;

namespace HarryTools.Audio {
    /// <summary> A clip with a selection weight. </summary>
    [Serializable]
    public struct ClipEntry {
        // ==================== VARIABLES ===================
        [Tooltip("Audio clip to play.")]
        public AudioClip Clip;
        [Min(0), Tooltip("Relative chance of being picked when the selection mode is random. 0 never picks it.")]
        public float Weight;

        // ==================== CONSTRUCTORS ===================
        public ClipEntry(AudioClip _clip, float _weight = 1f) {
            Clip = _clip;
            Weight = _weight;
        }
    }
}
