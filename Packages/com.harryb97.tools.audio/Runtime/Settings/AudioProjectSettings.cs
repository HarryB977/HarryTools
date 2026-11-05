using System.Collections.Generic;
using HarryTools.DomainReload;
using UnityEngine;
using UnityEngine.Audio;

namespace HarryTools.Audio {
    /// <summary> Project-wide audio configuration. Lives at Resources/HarryTools/AudioProjectSettings.asset. </summary>
    public sealed class AudioProjectSettings : ScriptableObject {
        // ==================== VARIABLES ===================
        private const string ResourcePath = "HarryTools/AudioProjectSettings";

        [ClearOnReload] private static AudioProjectSettings instance;

        [Header("Mixer")]
        [Tooltip("Mixer with one exposed volume parameter per bus. Leave empty to multiply volumes on the audio sources instead.")]
        public AudioMixer Mixer;
        [SerializeField] private List<BusConfig> buses = new List<BusConfig> {
            new BusConfig(AudioBus.Master, "MasterVolume"),
            new BusConfig(AudioBus.Music, "MusicVolume"),
            new BusConfig(AudioBus.SFX, "SFXVolume"),
            new BusConfig(AudioBus.Ambience, "AmbienceVolume"),
            new BusConfig(AudioBus.Voice, "VoiceVolume"),
            new BusConfig(AudioBus.UI, "UIVolume"),
        };

        [Header("Catalog")]
        [Tooltip("Catalog used to resolve audio references at runtime.")]
        public AudioCatalog Catalog;

        [Header("Voices")]
        [Min(1), Tooltip("Hard limit of simultaneous voices.")]
        public int MaxVoices = 32;
        [Min(0), Tooltip("Voices created up front.")]
        public int PrewarmVoices = 16;
        [Min(1), Tooltip("Maximum number of voices sounding at the same time.")]
        public int MaxSimulated = 10;

        [Header("Mix")]
        [SerializeField] private List<DuckingRule> ducking = new List<DuckingRule> {
            new DuckingRule(AudioBus.Voice, AudioBus.Music, -6f, 0.2f, 0.6f),
        };
        [Min(0f), Tooltip("Default music crossfade duration in seconds.")]
        public float DefaultCrossfade = 1f;

        [Header("Code generation")]
        [Tooltip("Folder whose subfolders become the groups of the generated id classes.")]
        public string CategoryRootFolder = "Assets/Audio";
        [Tooltip("Path of the generated ids file.")]
        public string GeneratedFilePath = "Assets/HarryTools/Audio/AudioIds.g.cs";
        [Tooltip("Namespace of the generated ids. Empty for the global namespace.")]
        public string GeneratedNamespace = "";

        [Header("Persistence")]
        [Tooltip("PlayerPrefs key used to store bus volumes.")]
        public string PersistenceKey = "HarryTools.Audio.Volumes";

        // ==================== PROPERTIES ===================
        /// <summary> Settings loaded from Resources, or null when the project has none. </summary>
        public static AudioProjectSettings Instance {
            get {
                if (instance == null) instance = Resources.Load<AudioProjectSettings>(ResourcePath);
                return instance;
            }
        }

        public IReadOnlyList<BusConfig> Buses => buses;
        public IReadOnlyList<DuckingRule> Ducking => ducking;

        // ==================== INTERNAL ===================
        internal void SetBuses(List<BusConfig> _buses) => buses = _buses;
        internal void SetDucking(List<DuckingRule> _ducking) => ducking = _ducking;
    }
}
