namespace HarryTools.TextEffects {
    public enum IssueSeverity {
        Warning,
        Error
    }

    /// <summary> A problem found while parsing the markup </summary>
    public readonly struct TextIssue {
        public IssueSeverity Severity { get; }
        public string Message { get; }

        /// <summary> Position in the original text, markup included </summary>
        public int Position { get; }

        public TextIssue(IssueSeverity _severity, string _message, int _position) {
            Severity = _severity;
            Message = _message;
            Position = _position;
        }
    }
}
