using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace HarryTools.Audio {
    /// <summary> Common data of every playable audio asset. </summary>
    public abstract class AudioAsset : AudioCatalogItem {
        // ==================== VARIABLES ===================
        [Header("Clips")]
        [SerializeField] private List<ClipEntry> clips = new List<ClipEntry>();
        [SerializeField, Tooltip("How the next clip is chosen on each play.")]
        private ClipSelection selection = ClipSelection.RandomNoRepeat;

        [Header("Variation")]
        [SerializeField, Tooltip("Random volume range applied on each play (min, max).")]
        private Vector2 volumeRange = Vector2.one;
        [SerializeField, Tooltip("Random pitch range applied on each play (min, max).")]
        private Vector2 pitchRange = Vector2.one;

        [Header("Timing and limits")]
        [SerializeField, Min(0), Tooltip("Seconds to wait before the sound starts.")]
        private float delay;
        [SerializeField, Min(0), Tooltip("Minimum seconds between two plays of this asset.")]
        private float cooldown;
        [SerializeField, Min(0), Tooltip("Maximum simultaneous instances. 0 = unlimited.")]
        private int maxInstances;
        [SerializeField, Tooltip("What to do when MaxInstances is reached.")]
        private InstanceLimitPolicy limitPolicy = InstanceLimitPolicy.StealOldest;
        [SerializeField, Range(0, 100), Tooltip("Higher is more important when voices are stolen.")]
        private int priority = 50;

        [Header("Output")]
        [SerializeField] private AudioBus bus;
        [SerializeField, Tooltip("Mixer group used instead of the bus group.")]
        private AudioMixerGroup outputOverride;

        [Header("Spatial")]
        [SerializeField, Tooltip("When set, overrides the inline spatial settings.")]
        private SpatialProfile spatialProfile;
        [SerializeField] private SpatialSettings spatial = SpatialSettings.Default2D;

        [Header("Playback")]
        [SerializeField] private bool loop;
        [SerializeField, Min(0)] private float fadeIn;
        [SerializeField, Min(0)] private float fadeOut;
        [SerializeField, Tooltip("Keep playing while the game is paused.")]
        private bool ignorePause;
        [SerializeField, Tooltip("Scale pitch with Time.timeScale.")]
        private bool followTimeScale;

        // ==================== CONSTRUCTORS ===================
        protected AudioAsset(AudioBus _bus, bool _loop, int _priority) {
            bus = _bus;
            loop = _loop;
            priority = _priority;
        }

        // ==================== PROPERTIES ===================
        public IReadOnlyList<ClipEntry> Clips => clips;
        public ClipSelection Selection => selection;
        public Vector2 VolumeRange => volumeRange;
        public Vector2 PitchRange => pitchRange;
        public float Delay => delay;
        public float Cooldown => cooldown;
        public int MaxInstances => maxInstances;
        public InstanceLimitPolicy LimitPolicy => limitPolicy;
        public int Priority => priority;
        public AudioBus Bus => bus;
        public AudioMixerGroup OutputOverride => outputOverride;
        public SpatialProfile SpatialProfile => spatialProfile;
        public SpatialSettings Spatial => spatial;
        public bool Loop => loop;
        public float FadeIn => fadeIn;
        public float FadeOut => fadeOut;
        public bool IgnorePause => ignorePause;
        public bool FollowTimeScale => followTimeScale;

        // ==================== PUBLIC ===================
        /// <summary> Effective spatial settings: the profile wins over the inline values. </summary>
        public SpatialSettings GetSpatial() => spatialProfile != null ? spatialProfile.Settings : spatial;

        // ==================== INTERNAL ===================
        internal void SetClips(IList<ClipEntry> _entries) {
            clips.Clear();
            for (int _i = 0; _i < _entries.Count; _i++) clips.Add(_entries[_i]);
        }
        internal void SetSelection(ClipSelection _selection) => selection = _selection;
        internal void SetSpatial(SpatialSettings _spatial) => spatial = _spatial;
        internal void SetSpatialProfile(SpatialProfile _profile) => spatialProfile = _profile;
    }
}
