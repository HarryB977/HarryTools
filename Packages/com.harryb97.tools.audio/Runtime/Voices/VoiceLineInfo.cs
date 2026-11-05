namespace HarryTools.Audio {
    /// <summary> A voice line passed to VoiceChannel.LineStarted and LineEnded. </summary>
    public readonly struct VoiceLineInfo {
        // ==================== CONSTRUCTORS ===================
        internal VoiceLineInfo(VoiceAsset _voice, SpeakerAsset _speaker, AudioEmitter _emitter, float _duration, AudioHandle _handle) {
            Voice = _voice;
            Speaker = _speaker;
            Emitter = _emitter;
            Duration = _duration;
            Handle = _handle;
        }

        // ==================== PROPERTIES ===================
        public VoiceAsset Voice { get; }
        public SpeakerAsset Speaker { get; }
        /// <summary> Emitter that says the line, or null when it plays in 2D. </summary>
        public AudioEmitter Emitter { get; }
        /// <summary> Seconds the chosen clip lasts at its pitch: clip length / |pitch|. </summary>
        public float Duration { get; }
        public AudioHandle Handle { get; }
    }
}
