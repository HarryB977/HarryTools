namespace HarryTools.Audio {
    /// <summary> Reverb an acoustic module wants for one voice. The core only applies these numbers; it knows nothing about rooms or rays. </summary>
    public struct AcousticReverb {
        // ==================== VARIABLES ===================
        /// <summary> Reverb amount, 0 to 1. 0 means no reverb. </summary>
        public float Wet;
        /// <summary> Decay time in seconds, 0.1 to 20. </summary>
        public float DecayTime;
        /// <summary> Decay time of the high frequencies relative to DecayTime, 0.1 to 2. </summary>
        public float HighFrequencyRatio;
        /// <summary> Delay of the first reflections in seconds, 0 to 0.3. </summary>
        public float ReflectionsDelay;
    }
}
