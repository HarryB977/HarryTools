namespace HarryTools.Dialogue {
    /// <summary> One visible option of a <see cref="ChoicePrompt"/>, with its state when the choice was offered </summary>
    public sealed class ChoiceOptionState {
        // ==================== VARIABLES ===================
        #region Public Variables
        public ChoiceOption Option { get; }

        /// <summary> The option can be chosen. When false it is shown blocked </summary>
        public bool Available { get; }

        /// <summary> The option was chosen before and is marked as seen </summary>
        public bool Seen { get; }

        /// <summary> The option the timer falls back to when nothing is selected </summary>
        public bool IsDefault { get; }
        #endregion

        // ==================== START ====================
        internal ChoiceOptionState(ChoiceOption _option, bool _available, bool _seen) {
            Option = _option;
            Available = _available;
            Seen = _seen;
            IsDefault = _option.IsDefault;
        }
    }
}
