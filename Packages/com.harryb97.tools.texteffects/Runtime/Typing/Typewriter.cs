using System;

namespace HarryTools.TextEffects {
    /// <summary> Time-driven character reveal with point commands. Knows nothing about TextMeshPro or meshes </summary>
    public sealed class Typewriter {
        // ==================== VARIABLES ===================
        #region Public Variables
        public float CharactersPerSecond { get; set; } = 30f;
        public int VisibleCount { get; private set; }
        public TypewriterStatus Status { get; private set; } = TypewriterStatus.Idle;

        /// <summary> Ordinal of the next event to be notified (events already notified, or suppressed by Start) </summary>
        public int NextEventOrdinal { get; private set; }
        #endregion

        #region Private Variables
        private TextCommand[] commands = Array.Empty<TextCommand>();
        private int[] commandChars = Array.Empty<int>();
        private int commandCount;
        private int[] instantEnds = Array.Empty<int>();
        private int characterCount;

        private int nextCommand;
        private int suppressBelowOrdinal;
        private double budget;
        private float speed = 1f;
        #endregion

        // ==================== METHODS ====================
        #region Load
        /// <summary> Resolves commands and instant ranges to character indices. Allocates. '_characterSourceIndices[i]' is the TextMeshPro text position of character i </summary>
        public void Load(ParsedText _parsed, int[] _characterSourceIndices, int _characterCount) {
            characterCount = _characterCount;
            commandCount = _parsed.Commands.Count;

            if (commands.Length < commandCount) {
                commands = new TextCommand[commandCount];
                commandChars = new int[commandCount];
            }

            for (int i = 0; i < commandCount; i++) {
                TextCommand command = _parsed.Commands[i];
                commands[i] = command;
                commandChars[i] = FirstCharacterAtOrAfter(_characterSourceIndices, _characterCount, command.SourceIndex);
            }

            instantEnds = new int[_characterCount];
            for (int i = 0; i < _parsed.Spans.Count; i++) {
                EffectSpan span = _parsed.Spans[i];
                if (span.Kind != SpanKind.Instant) continue;

                int first = FirstCharacterAtOrAfter(_characterSourceIndices, _characterCount, span.Start);
                int end = FirstCharacterAtOrAfter(_characterSourceIndices, _characterCount, span.End);
                if (end > first && first < _characterCount && end > instantEnds[first]) instantEnds[first] = end;
            }

            VisibleCount = 0;
            NextEventOrdinal = 0;
            nextCommand = 0;
            budget = 0;
            speed = 1f;
            Status = TypewriterStatus.Idle;
        }

        private static int FirstCharacterAtOrAfter(int[] _sourceIndices, int _count, int _position) {
            int low = 0;
            int high = _count;
            while (low < high) {
                int middle = (low + high) >> 1;
                if (_sourceIndices[middle] < _position) low = middle + 1;
                else high = middle;
            }
            return low;
        }
        #endregion

        #region Control
        /// <summary> Restarts from the first character. Events with an ordinal below '_suppressEventsBelowOrdinal' are not notified </summary>
        public void Start(int _suppressEventsBelowOrdinal = 0) {
            VisibleCount = 0;
            nextCommand = 0;
            budget = 0;
            speed = 1f;
            suppressBelowOrdinal = _suppressEventsBelowOrdinal;
            NextEventOrdinal = _suppressEventsBelowOrdinal;
            Status = TypewriterStatus.Typing;
        }

        public void Continue() {
            if (Status != TypewriterStatus.WaitingInput) return;
            budget = 0;
            Status = TypewriterStatus.Typing;
        }

        /// <summary> Reveals everything without notifying characters, events or voices. Notifies completion </summary>
        public void ShowAllImmediate(ITypewriterListener _listener) {
            bool wasComplete = Status == TypewriterStatus.Complete;
            VisibleCount = characterCount;
            nextCommand = commandCount;
            Status = TypewriterStatus.Complete;
            if (!wasComplete) _listener?.OnCompleted();
        }
        #endregion

        #region Advance
        public void Tick(float _dt, ITypewriterListener _listener) {
            if (Status != TypewriterStatus.Typing) return;

            budget += _dt;
            while (Status == TypewriterStatus.Typing && budget >= 0) {
                if (!RunCommandsAt(VisibleCount, true, false, _listener)) return;
                if (budget < 0) break;
                if (VisibleCount >= characterCount) {
                    Complete(_listener);
                    break;
                }

                RevealNext(false, _listener);
                budget -= Interval();
            }
        }

        /// <summary> Reveals everything up to the next waitinput (which is respected) or the end, ignoring pauses </summary>
        public void Skip(ITypewriterListener _listener) {
            if (Status != TypewriterStatus.Typing) return;

            budget = 0;
            while (Status == TypewriterStatus.Typing) {
                if (!RunCommandsAt(VisibleCount, true, true, _listener)) return;
                if (VisibleCount >= characterCount) {
                    Complete(_listener);
                    break;
                }

                RevealNext(true, _listener);
            }
        }

        private double Interval() {
            double rate = (double)CharactersPerSecond * speed;
            return 1.0 / (rate > 0.0001 ? rate : 0.0001);
        }

        private void Complete(ITypewriterListener _listener) {
            Status = TypewriterStatus.Complete;
            _listener?.OnCompleted();
        }

        /// <summary> Shows the next character, or its whole instant block </summary>
        private void RevealNext(bool _skipped, ITypewriterListener _listener) {
            int first = VisibleCount;
            int end = first + 1;
            int blockEnd = instantEnds[first];
            if (blockEnd > end) {
                end = blockEnd;
                for (int k = first + 1; k < end; k++) {
                    if (instantEnds[k] > end) end = instantEnds[k];
                }
            }

            for (int i = first; i < end; i++) {
                // Commands inside an instant block run with it, but they cannot pause or wait
                if (i > first) RunCommandsAt(i, false, _skipped, _listener);
                VisibleCount = i + 1;
                _listener?.OnCharacterShown(i, _skipped);
            }
        }

        /// <summary> Runs every pending command placed before character '_character'. Returns false when it stopped for input </summary>
        private bool RunCommandsAt(int _character, bool _canStop, bool _skipped, ITypewriterListener _listener) {
            while (nextCommand < commandCount && commandChars[nextCommand] <= _character) {
                TextCommand command = commands[nextCommand];
                nextCommand++;

                switch (command.Type) {
                    case TextCommandType.Pause:
                        if (!_canStop || _skipped) break;
                        budget -= command.Value;
                        // Later commands wait for the pause to elapse
                        if (budget < 0) return true;
                        break;
                    case TextCommandType.Speed:
                        speed = command.Value;
                        break;
                    case TextCommandType.Voice:
                        _listener?.OnVoiceChanged(command.Name);
                        break;
                    case TextCommandType.Event:
                        if (command.EventOrdinal >= suppressBelowOrdinal) _listener?.OnEvent(command.Name, command.EventOrdinal, _skipped);
                        NextEventOrdinal = command.EventOrdinal + 1;
                        break;
                    case TextCommandType.WaitInput:
                        if (!_canStop) break;
                        Status = TypewriterStatus.WaitingInput;
                        _listener?.OnWaitingInput();
                        return false;
                }
            }
            return true;
        }
        #endregion
    }
}
