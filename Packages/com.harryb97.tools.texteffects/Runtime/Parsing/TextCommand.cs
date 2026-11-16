namespace HarryTools.TextEffects {
    /// <summary> Kind of point command found in the markup </summary>
    public enum TextCommandType {
        Pause,
        Speed,
        WaitInput,
        Event,
        Voice
    }

    /// <summary> A point command extracted from the markup. It applies before the first character whose index is at least 'SourceIndex' </summary>
    public readonly struct TextCommand {
        // ==================== VARIABLES ===================
        #region Public Variables
        public TextCommandType Type { get; }

        /// <summary> Position in the TextMeshPro text (markup removed) </summary>
        public int SourceIndex { get; }

        /// <summary> Seconds for Pause, multiplier for Speed. Zero otherwise </summary>
        public float Value { get; }

        /// <summary> Event or voice name. Null otherwise </summary>
        public string Name { get; }

        /// <summary> Order among the Event commands (0, 1, 2...). -1 for any other type </summary>
        public int EventOrdinal { get; }
        #endregion

        // ==================== START ====================
        public TextCommand(TextCommandType _type, int _sourceIndex, float _value, string _name, int _eventOrdinal) {
            Type = _type;
            SourceIndex = _sourceIndex;
            Value = _value;
            Name = _name;
            EventOrdinal = _eventOrdinal;
        }
    }
}
