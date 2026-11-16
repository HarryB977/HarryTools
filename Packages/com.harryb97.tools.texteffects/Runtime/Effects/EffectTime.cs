namespace HarryTools.TextEffects {
    /// <summary> Timing information passed to an effect for one character </summary>
    public readonly struct EffectTime {
        // ==================== VARIABLES ===================
        #region Public Variables
        /// <summary> Seconds since the effect started on this character </summary>
        public float Elapsed { get; }

        /// <summary> Normalized 0..1 progress without easing. Continuous effects receive 0 and use GlobalTime </summary>
        public float Progress { get; }

        /// <summary> Seconds since the text started running, shared by every character </summary>
        public float GlobalTime { get; }
        #endregion

        // ==================== START ====================
        public EffectTime(float _elapsed, float _progress, float _globalTime) {
            Elapsed = _elapsed;
            Progress = _progress;
            GlobalTime = _globalTime;
        }
    }
}
