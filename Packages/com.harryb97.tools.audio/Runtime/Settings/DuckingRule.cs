using System;
using UnityEngine;

namespace HarryTools.Audio {
    /// <summary> Lowers the target bus while the trigger bus is playing. </summary>
    [Serializable]
    public struct DuckingRule {
        [Tooltip("Bus that causes the ducking while it has playing voices.")]
        public AudioBus Trigger;
        [Tooltip("Bus that gets attenuated.")]
        public AudioBus Target;
        [Tooltip("Attenuation applied to the target, in dB (negative lowers the volume).")]
        public float AttenuationDb;
        [Min(0f), Tooltip("Seconds to reach the attenuation.")]
        public float Attack;
        [Min(0f), Tooltip("Seconds to return to normal after the trigger stops.")]
        public float Release;

        public DuckingRule(AudioBus _trigger, AudioBus _target, float _attenuationDb, float _attack, float _release) {
            Trigger = _trigger;
            Target = _target;
            AttenuationDb = _attenuationDb;
            Attack = _attack;
            Release = _release;
        }
    }
}
