using System;
using UnityEngine;

namespace HarryTools.Audio {
    /// <summary> A clip with a selection weight. A class so that rows added in the Inspector start with weight 1. </summary>
    [Serializable]
    public sealed class ClipEntry {
        // ==================== VARIABLES ===================
        [SerializeField, Tooltip("Audio clip to play.")]
        private AudioClip clip;
        [SerializeField, Min(0), Tooltip("Relative chance of being picked by Random and RandomNoRepeat. 0 never picks it. Ignored by Shuffle and Sequence.")]
        private float weight = 1f;

        // ==================== CONSTRUCTORS ===================
        public ClipEntry() { }

        public ClipEntry(AudioClip _clip, float _weight = 1f) {
            clip = _clip;
            weight = _weight;
        }

        // ==================== PROPERTIES ===================
        public AudioClip Clip => clip;
        public float Weight => weight;
    }
}
