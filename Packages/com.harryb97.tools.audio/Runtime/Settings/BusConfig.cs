using System;
using UnityEngine;
using UnityEngine.Audio;

namespace HarryTools.Audio {
    /// <summary> Maps a logical bus to its mixer group and the exposed volume parameter. A class so that rows added in the Inspector start at volume 1. </summary>
    [Serializable]
    public sealed class BusConfig {
        [Tooltip("Logical bus this entry configures.")]
        public AudioBus Bus;
        [Tooltip("Mixer group voices on this bus are routed to.")]
        public AudioMixerGroup Group;
        [Tooltip("Exposed mixer parameter that controls the bus volume in dB. Empty means the bus has no mixer volume.")]
        public string VolumeParameter;
        [Range(0f, 1f), Tooltip("Linear volume the bus starts with.")]
        public float DefaultVolume = 1f;

        // ==================== CONSTRUCTORS ===================
        public BusConfig() { }

        public BusConfig(AudioBus _bus, string _volumeParameter, float _defaultVolume = 1f) {
            Bus = _bus;
            Group = null;
            VolumeParameter = _volumeParameter;
            DefaultVolume = _defaultVolume;
        }
    }
}
