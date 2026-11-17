namespace HarryTools.Dialogue {
    /// <summary> Shows nothing: a line lasts its lifetime, or until advanced when it waits for the next line </summary>
    internal sealed class HeadlessLineChannel : ILineChannel {
        // ==================== VARIABLES ===================
        #region Public Variables
        public static readonly HeadlessLineChannel Instance = new HeadlessLineChannel();
        #endregion

        // ==================== METHODS ====================
        #region Public Methods
        public ILineHandle Present(LinePresentation _line) {
            return new Handle(_line.Run, _line.Lifetime.Resolve(TextLength(_line.Line)), _line.Advance);
        }

        /// <summary> Length of the localized text, or 0 when it is not set or cannot be resolved </summary>
        internal static int TextLength(LineNode _line) {
            return Dialogue.ResolveLineText(_line)?.Length ?? 0;
        }
        #endregion

        // ==================== HANDLE ====================
        #region Handle
        private sealed class Handle : ILineHandle {
            private readonly DialogueRun run;
            private readonly float duration;
            private readonly AdvanceMode advance;
            private float startClock;
            private float pausedClock;
            private bool paused;
            private bool advanced;
            private bool closed;

            public Handle(DialogueRun _run, float _duration, AdvanceMode _advance) {
                run = _run;
                duration = _duration;
                advance = _advance;
                startClock = _run.Clock;
            }

            private float Elapsed => (paused ? pausedClock : run.Clock) - startClock;

            public bool IsFinished => closed || advanced || (advance != AdvanceMode.Input && Elapsed >= duration);

            public void Advance() {
                // An automatic line that would never end still accepts input
                if (advance == AdvanceMode.Auto && !float.IsPositiveInfinity(duration)) return;
                advanced = true;
            }

            public void Skip() {
                closed = true;
            }

            public void SetPaused(bool _paused) {
                if (_paused == paused) return;
                paused = _paused;
                if (_paused) pausedClock = run.Clock;
                else startClock += run.Clock - pausedClock;
            }

            public void Release() {
                closed = true;
            }
        }
        #endregion
    }
}
