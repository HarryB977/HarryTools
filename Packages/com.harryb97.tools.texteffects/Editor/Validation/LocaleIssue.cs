namespace HarryTools.TextEffects.Editor {
    /// <summary> A markup problem found in the text of one locale </summary>
    public readonly struct LocaleIssue {
        // ==================== VARIABLES ===================
        #region Public Variables
        public string Locale { get; }
        public TextIssue Issue { get; }
        #endregion

        // ==================== START ====================
        public LocaleIssue(string _locale, TextIssue _issue) {
            Locale = _locale;
            Issue = _issue;
        }
    }
}
