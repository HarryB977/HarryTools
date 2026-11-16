namespace HarryTools.TextEffects {
    /// <summary> Data of a character that just became visible </summary>
    public readonly struct CharacterShownArgs {
        // ==================== VARIABLES ===================
        #region Public Variables
        /// <summary> Character position in the displayed text (markup removed) </summary>
        public int Index { get; }
        public char Character { get; }

        /// <summary> Voice active at that character. Null when there is none </summary>
        public TypingVoice Voice { get; }

        /// <summary> True when the character was revealed by Skip instead of typed </summary>
        public bool Skipped { get; }
        #endregion

        // ==================== CONSTRUCTORS ===================
        public CharacterShownArgs(int _index, char _character, TypingVoice _voice, bool _skipped) {
            Index = _index;
            Character = _character;
            Voice = _voice;
            Skipped = _skipped;
        }
    }
}
