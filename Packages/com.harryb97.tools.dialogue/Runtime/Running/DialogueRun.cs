using System;
using System.Collections.Generic;
using HarryTools.Actions;
using HarryTools.Conditions;
using HarryTools.TextEffects;
using UnityEngine;
using Logger = HarryTools.LoggerTool.Logger;

namespace HarryTools.Dialogue {
    /// <summary> One dialogue in progress, created by <see cref="Dialogue.Start"/> </summary>
    public sealed class DialogueRun {
        // ==================== VARIABLES ===================
        #region Private Variables
        // Nodes a run may cross in one tick without waiting before it is treated as an endless loop
        private const int MaxStepsPerTick = 1000;

        private enum Phase {
            Enter,
            DelayBefore,
            BeforeActions,
            Showing,
            EndActions,
            DelayAfter,
            WaitActions,
            Choice
        }

        private readonly struct CallFrame {
            public readonly DialogueAsset Asset;
            public readonly JumpNode Jump;

            public CallFrame(DialogueAsset _asset, JumpNode _jump) {
                Asset = _asset;
                Jump = _jump;
            }
        }

        private readonly Stack<CallFrame> calls = new Stack<CallFrame>();
        private Phase phase;
        private float clock;
        private float waitUntil;
        private ActionHandle awaited;
        private ILineHandle lineHandle;
        private LinePresentation presentation;
        // Self of the current line's actions
        private GameObject lineActor;
        private RunState stateBeforePause;
        private bool warnedAmbientChoice;

        // Requests made while the run is ticking are applied once the current step is done
        private bool ticking;
        private bool stopRequested;
        private bool advanceRequested;
        private bool pauseRequested;
        private bool skipRequested;
        private ChoiceOption choiceRequested;

        // Runs, in order and one after the other, what a skip passes over
        private TextEventQueue skipQueue;
        private ParsedText skipParsed;
        #endregion

        #region Public Variables
        /// <summary> The dialogue that was started </summary>
        public DialogueAsset Asset { get; }

        /// <summary> The dialogue the current node belongs to. Differs from <see cref="Asset"/> after a jump </summary>
        public DialogueAsset CurrentAsset { get; private set; }

        public bool IsMain { get; }
        public RunState State { get; private set; } = RunState.Running;

        /// <summary> The node being run </summary>
        public DialogueNode Current { get; private set; }

        public DialogueTime Time { get; }

        /// <summary> The player may skip this dialogue </summary>
        public bool CanSkip => Asset.AllowSkip;

        /// <summary> True while the run moves through the graph without showing lines </summary>
        public bool IsSkipping { get; private set; }

        /// <summary> The choice waiting for an answer, or null </summary>
        public ChoicePrompt CurrentChoice { get; private set; }

        public event Action<DialogueRun, LineNode> LineStarted;
        public event Action<DialogueRun, LineNode> LineEnded;
        public event Action<DialogueRun, ChoicePrompt> ChoicePresented;

        /// <summary> Raised when an option is chosen, by the player, the timer or an ambient dialogue. Not raised for the no answer output </summary>
        public event Action<DialogueRun, ChoiceOption> ChoiceMade;

        public event Action<DialogueRun, EndReason> Ended;
        #endregion

        #region Internal Variables
        internal DialogueStartOptions Options { get; }

        /// <summary> What this run does, as an ambient run, when a main dialogue starts </summary>
        internal AmbientPolicy AmbientPolicy { get; }

        /// <summary> Seconds this run has ticked on its own clock. Frozen while paused </summary>
        internal float Clock => clock;
        #endregion

        #region Private Properties
        // Moving through the graph or waiting for a choice
        private bool IsActive => State == RunState.Running || State == RunState.WaitingChoice;
        #endregion

        // ==================== START ====================
        internal DialogueRun(DialogueAsset _asset, DialogueStartOptions _options) {
            Asset = _asset;
            CurrentAsset = _asset;
            Options = _options;
            IsMain = _options.IsMain;
            Time = _options.Time ?? (_asset.OverrideTime ? _asset.Time : DialogueSettings.Current.DefaultTime);
            AmbientPolicy = _options.AmbientPolicy ?? (_asset.OverrideAmbientPolicy ? _asset.AmbientPolicy : DialogueSettings.Current.DefaultAmbientPolicy);
            MoveTo(_asset.Start);
        }

        // ==================== CONTROL ====================
        #region Control
        /// <summary> Player input for the line on screen </summary>
        public void Advance() {
            if (State != RunState.Running) return;
            if (ticking) {
                advanceRequested = true;
                return;
            }
            lineHandle?.Advance();
        }

        /// <summary> Freezes the run, its timers and its line. Actions already running are not paused </summary>
        public void Pause() {
            if (State == RunState.Ended || State == RunState.Paused) return;
            if (ticking) {
                pauseRequested = true;
                return;
            }
            ApplyPause();
        }

        public void Resume() {
            if (ticking) {
                pauseRequested = false;
                return;
            }
            if (State != RunState.Paused) return;
            State = stateBeforePause;
            lineHandle?.SetPaused(false);
        }

        /// <summary> Moves on without showing lines, running what they would have run, up to the next choice or the end. Ignored when the dialogue cannot be skipped </summary>
        public void Skip() {
            if (!CanSkip || State != RunState.Running || IsSkipping) return;
            skipRequested = true;
            if (ticking) return;
            RunSteps();
        }

        /// <summary> Ends the run with <see cref="EndReason.Stopped"/> </summary>
        public void Stop() {
            if (State == RunState.Ended) return;
            if (ticking) {
                stopRequested = true;
                return;
            }
            Finish(EndReason.Stopped);
        }
        #endregion

        // ==================== TICK ====================
        #region Tick
        /// <summary> Moves through the graph until something has to wait </summary>
        internal void Tick(float _scaledDt, float _unscaledDt) {
            if (ticking || !IsActive) return;
            clock += Time == DialogueTime.Scaled ? _scaledDt : _unscaledDt;
            RunSteps();
        }

        /// <summary> Applies pending requests, then steps until the run has to wait </summary>
        private void RunSteps() {
            ticking = true;
            try {
                if (ApplyRequests()) return;
                for (int _steps = 0; ; _steps++) {
                    if (_steps >= MaxStepsPerTick) {
                        Logger.LogWarning(Dialogue.LogCategory, $"Dialogue '{CurrentAsset.name}' crossed {MaxStepsPerTick} nodes in one frame without waiting. Stopped to avoid an endless loop.");
                        Finish(EndReason.Stopped);
                        break;
                    }
                    bool _moved = Step();
                    if (ApplyRequests() || !IsActive) break;
                    // A skip requested during the step has moved the run on already
                    if (!_moved && !IsSkipping) break;
                }
            } finally {
                ticking = false;
            }
        }

        /// <summary> Applies what was asked during the last step </summary>
        /// <returns> True when the run must stop moving this tick </returns>
        private bool ApplyRequests() {
            if (State == RunState.Ended) return true;
            if (skipRequested) {
                skipRequested = false;
                if (State == RunState.Running && !IsSkipping) BeginSkip();
                if (State == RunState.Ended) return true;
            }
            if (stopRequested) {
                Finish(EndReason.Stopped);
                return true;
            }
            if (advanceRequested) {
                advanceRequested = false;
                lineHandle?.Advance();
            }
            if (pauseRequested) {
                pauseRequested = false;
                ApplyPause();
                return true;
            }
            return false;
        }

        /// <summary> Runs one step of the current node </summary>
        /// <returns> True when the run moved on, false when it has to wait </returns>
        private bool Step() {
            switch (phase) {
                case Phase.Enter:
                    if (IsSkipping) SkipThrough(Current);
                    else Enter(Current);
                    return true;

                case Phase.DelayBefore:
                    if (clock < waitUntil) return false;
                    awaited = RunActions(((LineNode)Current).Before.Actions, lineActor);
                    phase = Phase.BeforeActions;
                    return true;

                case Phase.BeforeActions:
                    if (IsAwaiting()) return false;
                    PresentLine();
                    return true;

                case Phase.Showing:
                    if (!lineHandle.IsFinished) return false;
                    EndLine();
                    return true;

                case Phase.EndActions:
                    if (IsAwaiting()) return false;
                    waitUntil = clock + ((LineNode)Current).DelayAfter;
                    phase = Phase.DelayAfter;
                    return true;

                case Phase.DelayAfter:
                    if (clock < waitUntil) return false;
                    GoTo(((LineNode)Current).Next);
                    return true;

                case Phase.WaitActions:
                    if (IsAwaiting()) return false;
                    GoTo(((ActionsNode)Current).Next);
                    return true;

                case Phase.Choice:
                    if (choiceRequested != null) {
                        ChoiceOption _option = choiceRequested;
                        choiceRequested = null;
                        TakeOption(_option);
                        return true;
                    }
                    if (!CurrentChoice.TimedOut) return false;
                    TimeOut();
                    return true;

                default:
                    return false;
            }
        }

        private bool IsAwaiting() {
            if (awaited != null && awaited.IsRunning) return true;
            awaited = null;
            return false;
        }
        #endregion

        // ==================== NODES ====================
        #region Nodes
        private void Enter(DialogueNode _node) {
            switch (_node) {
                case StartNode _start:
                    GoTo(_start.Next);
                    break;

                case LineNode _line:
                    lineActor = null;
                    waitUntil = clock + _line.DelayBefore;
                    phase = Phase.DelayBefore;
                    break;

                case ActionsNode _actions:
                    ActionHandle _handle = RunActions(_actions.Actions, null);
                    if (_handle != null && _actions.WaitForCompletion) {
                        awaited = _handle;
                        phase = Phase.WaitActions;
                    } else {
                        GoTo(_actions.Next);
                    }
                    break;

                case BranchNode _branch:
                    GoTo(PickCase(_branch));
                    break;

                case ChoiceNode _choice:
                    Offer(_choice);
                    break;

                case JumpNode _jump:
                    Jump(_jump);
                    break;

                case EndNode _:
                    EndOrReturn();
                    break;

                default:
                    Logger.LogWarning(Dialogue.LogCategory, $"Dialogue '{CurrentAsset.name}': {_node.GetType().Name} is not supported here. The dialogue ends.");
                    EndOrReturn();
                    break;
            }
        }

        private NodeOutput PickCase(BranchNode _branch) {
            LogicContext _context = Context(null);
            for (int _i = 0; _i < _branch.Cases.Count; _i++) {
                BranchCase _case = _branch.Cases[_i];
                if (_case == null) continue;
                if (_case.Conditions == null || _case.Conditions.Check(_context)) return _case.Next;
            }
            return _branch.Otherwise;
        }

        private void Jump(JumpNode _jump) {
            DialogueAsset _target = _jump.Target;
            if (_target == null || _target.Start == null) {
                Logger.LogWarning(Dialogue.LogCategory, $"Dialogue '{CurrentAsset.name}': jump node without a target dialogue or with a target without start node.");
                if (_jump.Mode == JumpMode.CallAndReturn) GoTo(_jump.Next);
                else EndOrReturn();
                return;
            }

            if (_jump.Mode == JumpMode.CallAndReturn) calls.Push(new CallFrame(CurrentAsset, _jump));
            CurrentAsset = _target;
            MoveTo(_target.Start);
        }
        #endregion

        // ==================== LINES ====================
        #region Lines
        private void PresentLine() {
            LineNode _line = (LineNode)Current;
            DialogueSettings _settings = DialogueSettings.Current;
            LineLifetime _lifetime = _line.OverrideLifetime ? _line.Lifetime : _settings.DefaultLifetime;
            AdvanceMode _advance = _line.OverrideAdvance ? _line.Advance : _settings.DefaultAdvance;

            presentation = new LinePresentation(this, _line, lineActor, _lifetime, _advance, Time);
            ILineChannel _channel = Dialogue.ResolveChannel(presentation);
            lineHandle = _channel.Present(presentation) ?? HeadlessLineChannel.Instance.Present(presentation);
            phase = Phase.Showing;

            LineStarted?.Invoke(this, _line);
            Dialogue.RaiseLineStarted(this, _line);
            RunActions(_line.OnStart.Actions, lineActor);
        }

        private void EndLine() {
            LineNode _line = (LineNode)Current;
            ReleaseLine();
            awaited = RunActions(_line.OnEnd.Actions, lineActor);
            phase = Phase.EndActions;
        }

        /// <summary> Lets go of the line on screen, if any, and raises LineEnded </summary>
        private void ReleaseLine() {
            if (lineHandle == null) return;
            ILineHandle _handle = lineHandle;
            LineNode _line = presentation.Line;
            lineHandle = null;
            presentation = null;

            _handle.Release();
            LineEnded?.Invoke(this, _line);
            Dialogue.RaiseLineEnded(this, _line);
        }
        #endregion

        // ==================== CHOICES ====================
        #region Choices
        /// <summary> Shows the choice in a main run. An ambient run answers at once </summary>
        private void Offer(ChoiceNode _node) {
            ChoicePrompt _prompt = new ChoicePrompt(this, _node, Context(null));

            if (!IsMain) {
                if (!warnedAmbientChoice) {
                    warnedAmbientChoice = true;
                    Logger.LogWarning(Dialogue.LogCategory, $"Dialogue '{CurrentAsset.name}' reached a choice in an ambient run. Choices belong to main dialogues: the default option is taken.");
                }
                ChoiceOption _option = _prompt.Fallback();
                if (_option != null) TakeOption(_option);
                else TakeNoOption(_node);
                return;
            }

            if (!_prompt.HasAvailable) {
                TakeNoOption(_node);
                return;
            }

            CurrentChoice = _prompt;
            State = RunState.WaitingChoice;
            phase = Phase.Choice;
            ChoicePresented?.Invoke(this, _prompt);
            Dialogue.RaiseChoicePresented(this, _prompt);
            Dialogue.ChoiceShown?.Invoke(_prompt);
        }

        /// <summary> Called by the open prompt. Applied at once, or after the current step when the run is ticking </summary>
        internal void RequestChoice(ChoicePrompt _prompt, ChoiceOption _option) {
            if (State != RunState.WaitingChoice || _prompt != CurrentChoice || choiceRequested != null) return;
            if (ticking) {
                choiceRequested = _option;
                return;
            }

            ticking = true;
            try {
                TakeOption(_option);
            } finally {
                ticking = false;
            }
            if (skipRequested) RunSteps();
            else ApplyRequests();
        }

        private void TimeOut() {
            ChoicePrompt _prompt = CurrentChoice;
            ChoiceOption _option = _prompt.Node.TimeoutRule == TimeoutRule.ChooseSelected ? _prompt.Fallback() : null;
            if (_option != null) {
                TakeOption(_option);
                return;
            }
            CloseChoice();
            GoTo(_prompt.Node.NoAnswer);
        }

        /// <summary> Remembers the option, runs its actions without waiting and follows it </summary>
        private void TakeOption(ChoiceOption _option) {
            CloseChoice();
            DialogueMemory.MarkChosen(CurrentAsset, _option.Id);
            ChoiceMade?.Invoke(this, _option);
            Dialogue.RaiseChoiceMade(this, _option);
            RunActions(_option.OnChosen, null);
            GoTo(_option.Next);
        }

        /// <summary> No option can be chosen: the no answer output, or the end with a warning when it is not connected </summary>
        private void TakeNoOption(ChoiceNode _node) {
            if (!_node.NoAnswer.IsConnected) {
                Logger.LogWarning(Dialogue.LogCategory, $"Dialogue '{CurrentAsset.name}': a choice has no option to offer and its no answer output is not connected. The dialogue ends there.");
            }
            GoTo(_node.NoAnswer);
        }

        /// <summary> Closes the open choice, if any, and lets the run move again </summary>
        private void CloseChoice() {
            choiceRequested = null;
            if (CurrentChoice == null) return;
            ChoicePrompt _prompt = CurrentChoice;
            CurrentChoice = null;
            _prompt.IsOpen = false;
            if (State == RunState.WaitingChoice) State = RunState.Running;
            Dialogue.ChoiceHidden?.Invoke(_prompt);
        }
        #endregion

        // ==================== SKIP ====================
        #region Skip
        /// <summary> Leaves the current node the way a skip does. The visible line is skipped and released; actions being awaited keep running unawaited </summary>
        private void BeginSkip() {
            IsSkipping = true;
            switch (phase) {
                case Phase.DelayBefore:
                    // The line has not started yet: it is skipped whole
                    phase = Phase.Enter;
                    break;

                case Phase.BeforeActions: {
                    LineNode _line = (LineNode)Current;
                    awaited = null;
                    EnqueueSkipped(_line.OnStart);
                    EnqueueTextEvents(_line);
                    EnqueueSkipped(_line.OnEnd);
                    GoTo(_line.Next);
                    break;
                }

                case Phase.Showing: {
                    // The line fires its own skipped text events as it is skipped
                    LineNode _line = (LineNode)Current;
                    lineHandle.Skip();
                    ReleaseLine();
                    EnqueueSkipped(_line.OnEnd);
                    GoTo(_line.Next);
                    break;
                }

                case Phase.EndActions:
                    awaited = null;
                    GoTo(((LineNode)Current).Next);
                    break;

                case Phase.DelayAfter:
                    GoTo(((LineNode)Current).Next);
                    break;

                case Phase.WaitActions:
                    awaited = null;
                    GoTo(((ActionsNode)Current).Next);
                    break;
            }
        }

        /// <summary> Crosses the node without showing or waiting. Lines and actions nodes queue their actions; a choice offered to the player ends the skip </summary>
        private void SkipThrough(DialogueNode _node) {
            switch (_node) {
                case LineNode _line:
                    lineActor = null;
                    EnqueueSkipped(_line.Before);
                    EnqueueSkipped(_line.OnStart);
                    EnqueueTextEvents(_line);
                    EnqueueSkipped(_line.OnEnd);
                    GoTo(_line.Next);
                    break;

                case ActionsNode _actions:
                    if (_actions.RunWhenSkipped) Enqueue(_actions.Actions, null);
                    GoTo(_actions.Next);
                    break;

                case ChoiceNode _choice:
                    Offer(_choice);
                    if (State == RunState.WaitingChoice) IsSkipping = false;
                    break;

                default:
                    Enter(_node);
                    break;
            }
        }

        private void EnqueueSkipped(SkippableActions _actions) {
            if (_actions != null && _actions.RunWhenSkipped) Enqueue(_actions.Actions, lineActor);
        }

        /// <summary> Queues the events of the line's markup, in text order, from its current text </summary>
        private void EnqueueTextEvents(LineNode _line) {
            TextEventTable _table = _line.Events;
            if (_table == null || _table.Entries.Count == 0) return;
            string _text = Dialogue.ResolveLineText(_line);
            if (string.IsNullOrEmpty(_text)) return;

            if (skipParsed == null) skipParsed = new ParsedText();
            TextParser.Parse(_text, TextEffectsSettings.Current, skipParsed);
            List<TextCommand> _commands = skipParsed.Commands;
            for (int _i = 0; _i < _commands.Count; _i++) {
                if (_commands[_i].Type != TextCommandType.Event) continue;
                if (!_table.TryGetEvent(_commands[_i].Name, out ActionList _actions, out bool _runWhenSkipped)) continue;
                if (_runWhenSkipped) Enqueue(_actions, lineActor);
            }
        }

        /// <summary> Queued actions run one after the other without holding the run </summary>
        private void Enqueue(ActionList _actions, GameObject _self) {
            if (_actions == null || _actions.IsEmpty) return;
            if (skipQueue == null) skipQueue = new TextEventQueue();
            skipQueue.Enqueue(_actions, Context(_self));
        }
        #endregion

        // ==================== FLOW ====================
        #region Flow
        private void MoveTo(DialogueNode _node) {
            Current = _node;
            phase = Phase.Enter;
        }

        /// <summary> Follows the output, or ends when it is not connected </summary>
        private void GoTo(NodeOutput _output) {
            if (_output == null || !_output.IsConnected) {
                EndOrReturn();
                return;
            }

            DialogueNode _node = CurrentAsset.Find(_output.Target);
            if (_node == null) {
                Logger.LogWarning(Dialogue.LogCategory, $"Dialogue '{CurrentAsset.name}': an output points to a missing node. The dialogue ends there.");
                EndOrReturn();
                return;
            }
            MoveTo(_node);
        }

        /// <summary> Returns to the last called jump node, or completes the run </summary>
        private void EndOrReturn() {
            if (calls.Count == 0) {
                Finish(IsSkipping ? EndReason.Skipped : EndReason.Completed);
                return;
            }

            CallFrame _frame = calls.Pop();
            CurrentAsset = _frame.Asset;
            GoTo(_frame.Jump.Next);
        }

        private void ApplyPause() {
            stateBeforePause = State;
            State = RunState.Paused;
            lineHandle?.SetPaused(true);
        }

        private void Finish(EndReason _reason) {
            if (State == RunState.Ended) return;
            State = RunState.Ended;
            IsSkipping = false;
            stopRequested = false;
            advanceRequested = false;
            pauseRequested = false;
            skipRequested = false;
            calls.Clear();

            // The skip queue is left running: what it holds was already happening when the run ended

            if (awaited != null && awaited.IsRunning) awaited.Cancel();
            awaited = null;
            ReleaseLine();
            CloseChoice();

            Dialogue.Unregister(this);
            Ended?.Invoke(this, _reason);
            Dialogue.RaiseEnded(this, _reason);
        }

        private ActionHandle RunActions(ActionList _actions, GameObject _self) {
            if (_actions == null || _actions.IsEmpty) return null;
            return _actions.Run(Context(_self));
        }

        private LogicContext Context(GameObject _self) {
            return new LogicContext(_self, Options.Origin);
        }
        #endregion
    }
}
