namespace HarryTools.TextEffects {
    /// <summary> An effect resolved to a range of TextMeshPro characters </summary>
    internal sealed class ResolvedSpan {
        // ==================== VARIABLES ===================
        #region Public Variables
        public TextEffect Effect { get; }

        /// <summary> First character index </summary>
        public int First { get; }

        /// <summary> Exclusive end character index </summary>
        public int End { get; }

        public int Length => End - First;

        /// <summary> One-shot effects: time at which the first character of the range became visible. -1 while it has not </summary>
        public float StartTime { get; set; } = -1f;
        #endregion

        // ==================== START ====================
        public ResolvedSpan(TextEffect _effect, int _first, int _end) {
            Effect = _effect;
            First = _first;
            End = _end;
        }

        // ==================== METHODS ====================
        public bool Contains(int _index) => _index >= First && _index < End;
    }
}
