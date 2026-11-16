using System.Collections.Generic;

namespace HarryTools.TextEffects {
    /// <summary> Result of parsing a text with markup. Instances can be reused through TextParser.Parse </summary>
    public sealed class ParsedText {
        // ==================== VARIABLES ===================
        #region Public Variables
        /// <summary> Original text, markup included </summary>
        public string Source { get; internal set; } = string.Empty;

        /// <summary> Text to give to TextMeshPro: our tags removed, everything else untouched </summary>
        public string TmpText { get; internal set; } = string.Empty;

        public List<TextCommand> Commands { get; } = new List<TextCommand>();
        public List<EffectSpan> Spans { get; } = new List<EffectSpan>();
        public List<TextIssue> Issues { get; } = new List<TextIssue>();

        /// <summary> Number of Event commands </summary>
        public int EventCount { get; internal set; }
        #endregion

        // ==================== METHODS ====================
        public void Clear() {
            Source = string.Empty;
            TmpText = string.Empty;
            Commands.Clear();
            Spans.Clear();
            Issues.Clear();
            EventCount = 0;
        }
    }
}
