using UnityEngine;

namespace HarryTools.Audio {
    /// <summary> A music track with an optional intro and loop points. </summary>
    [CreateAssetMenu(menuName = "HarryTools/Audio/Music", fileName = "Music")]
    public sealed class MusicAsset : AudioAsset {
        // ==================== VARIABLES ===================
        [Header("Music")]
        [SerializeField, Tooltip("Optional clip played once before the loop. Clips[0] is the loop.")]
        private AudioClip intro;
        [SerializeField, Tooltip("Loop between LoopStart and LoopEnd of Clips[0] instead of the whole clip.")]
        private bool useLoopPoints;
        [SerializeField, Min(0), Tooltip("Loop start in seconds.")]
        private double loopStart;
        [SerializeField, Min(0), Tooltip("Loop end in seconds.")]
        private double loopEnd;

        // ==================== CONSTRUCTORS ===================
        public MusicAsset() : base(AudioBus.Music, true, 100) { }

        // ==================== PROPERTIES ===================
        public AudioClip Intro => intro;
        public bool UseLoopPoints => useLoopPoints;
        public double LoopStart => loopStart;
        public double LoopEnd => loopEnd;
    }
}
