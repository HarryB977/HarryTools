using System.Collections.Generic;
using System.Text.RegularExpressions;
using HarryTools.Actions;
using HarryTools.Conditions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarryTools.Dialogue.Tests {
    public class ChoiceTests {
        // ==================== SETUP ====================
        #region Setup
        private readonly List<DialogueAsset> assets = new List<DialogueAsset>();
        private readonly List<EndReason> endReasons = new List<EndReason>();
        private readonly List<ChoicePrompt> presented = new List<ChoicePrompt>();
        private readonly List<ChoiceOption> made = new List<ChoiceOption>();
        private DialogueSettings settings;
        private FakeLineChannel channel;

        [SetUp]
        public void SetUp() {
            RecordingAction.Log.Clear();
            DialogueMemory.ClearForTests();
            settings = ScriptableObject.CreateInstance<DialogueSettings>();
            DialogueSettings.UseForTests(settings);
            Dialogue.ManualTick = true;
            channel = new FakeLineChannel();
            Dialogue.ChannelResolver = _ => channel;
            endReasons.Clear();
            presented.Clear();
            made.Clear();
            Dialogue.Ended += OnEnded;
            Dialogue.ChoicePresented += OnPresented;
            Dialogue.ChoiceMade += OnMade;
        }

        [TearDown]
        public void TearDown() {
            Dialogue.Ended -= OnEnded;
            Dialogue.ChoicePresented -= OnPresented;
            Dialogue.ChoiceMade -= OnMade;
            Dialogue.StopAll();
            Dialogue.ClearForTests();
            DialogueSettings.ClearForTests();
            DialogueMemory.ClearForTests();
            for (int _i = 0; _i < assets.Count; _i++) Object.DestroyImmediate(assets[_i]);
            assets.Clear();
            Object.DestroyImmediate(settings);
        }

        private void OnEnded(DialogueRun _run, EndReason _reason) => endReasons.Add(_reason);
        private void OnPresented(DialogueRun _run, ChoicePrompt _prompt) => presented.Add(_prompt);
        private void OnMade(DialogueRun _run, ChoiceOption _option) => made.Add(_option);
        #endregion

        // ==================== HELPERS ====================
        #region Helpers
        private DialogueAsset NewAsset() {
            DialogueAsset _asset = ScriptableObject.CreateInstance<DialogueAsset>();
            assets.Add(_asset);
            return _asset;
        }

        private static LineNode AddLine(DialogueAsset _asset, string _label) {
            LineNode _line = _asset.AddNode<LineNode>();
            _line.WriterNote = _label;
            return _line;
        }

        private static ChoiceNode AddChoice(DialogueAsset _asset, params ChoiceOption[] _options) {
            ChoiceNode _choice = _asset.AddNode<ChoiceNode>();
            _choice.Options.AddRange(_options);
            _asset.EnsureIds();
            return _choice;
        }

        private static ChoiceOption Option(bool _visible = true, bool _available = true, bool _isDefault = false, AfterChosen _after = AfterChosen.Nothing) {
            ChoiceOption _option = new ChoiceOption { IsDefault = _isDefault, AfterChosen = _after };
            if (!_visible) _option.Visible = Fixed(false);
            if (!_available) _option.Available = Fixed(false);
            return _option;
        }

        private static ConditionList Fixed(bool _result) {
            return new ConditionList(ConditionList.EvaluationMode.All, new FixedCondition(_result));
        }

        private static void Link(NodeOutput _output, DialogueNode _to) {
            _output.Target = _to.Id;
        }

        private static void Tick(float _dt = 0f) => Dialogue.TickAll(_dt, _dt);

        /// <summary> Start → choice with the given options </summary>
        private DialogueAsset WithChoice(out ChoiceNode _choice, params ChoiceOption[] _options) {
            DialogueAsset _asset = NewAsset();
            _choice = AddChoice(_asset, _options);
            Link(_asset.Start.Next, _choice);
            return _asset;
        }

        private static ChoiceOption[] Options(ChoicePrompt _prompt) {
            ChoiceOption[] _options = new ChoiceOption[_prompt.Options.Count];
            for (int _i = 0; _i < _options.Length; _i++) _options[_i] = _prompt.Options[_i].Option;
            return _options;
        }

        private static ChoiceOptionState StateOf(ChoicePrompt _prompt, ChoiceOption _option) {
            for (int _i = 0; _i < _prompt.Options.Count; _i++) {
                if (_prompt.Options[_i].Option == _option) return _prompt.Options[_i];
            }
            return null;
        }
        #endregion

        // ==================== CHOOSING ====================
        #region Choosing
        [Test]
        public void Choose_MarksChosen_RunsOnChosen_FollowsNext() {
            ChoiceOption _a = Option();
            ChoiceOption _b = Option();
            DialogueAsset _asset = WithChoice(out ChoiceNode _choice, _a, _b);
            LineNode _line = AddLine(_asset, "a");
            Link(_a.Next, _line);
            bool _markedBeforeAction = false;
            _a.OnChosen = new ActionList(
                new CallbackAction(() => _markedBeforeAction = DialogueMemory.WasChosen(_asset, _a.Id)),
                new RecordingAction("chosenA"));

            DialogueRun _run = Dialogue.Start(_asset);
            ChoicePrompt _runPresented = null;
            ChoiceOption _runMade = null;
            _run.ChoicePresented += (_, _prompt) => _runPresented = _prompt;
            _run.ChoiceMade += (_, _option) => _runMade = _option;
            Tick();

            ChoicePrompt _current = _run.CurrentChoice;
            Assert.IsNotNull(_current);
            Assert.AreSame(_run, _current.Run);
            Assert.AreSame(_choice, _current.Node);
            Assert.AreSame(_current, _runPresented);
            CollectionAssert.AreEqual(new[] { _current }, presented);
            Assert.AreEqual(RunState.WaitingChoice, _run.State);
            Assert.AreEqual(-1, _current.SelectedIndex);
            Assert.IsFalse(_current.HasTimer);
            CollectionAssert.AreEqual(new[] { _a, _b }, Options(_current));

            _current.Choose(0);

            Assert.IsTrue(DialogueMemory.WasChosen(_asset, _a.Id));
            Assert.IsFalse(DialogueMemory.WasChosen(_asset, _b.Id));
            Assert.IsTrue(_markedBeforeAction);
            Assert.IsTrue(RecordingAction.Log.Contains("chosenA"));
            Assert.AreSame(_a, _runMade);
            CollectionAssert.AreEqual(new[] { _a }, made);
            Assert.IsNull(_run.CurrentChoice);

            _current.Choose(1);
            CollectionAssert.AreEqual(new[] { _a }, made);

            Tick();
            Assert.AreSame(_line, _run.Current);
            Assert.AreEqual(RunState.Running, _run.State);
            Assert.IsTrue(RecordingAction.Log.Contains("present:a"));
        }

        [Test]
        public void Choose_InJumpedDialogue_MarksTargetDialogue() {
            ChoiceOption _a = Option();
            DialogueAsset _target = WithChoice(out _, _a);
            DialogueAsset _asset = NewAsset();
            JumpNode _jump = _asset.AddNode<JumpNode>();
            _jump.Target = _target;
            Link(_asset.Start.Next, _jump);

            DialogueRun _run = Dialogue.Start(_asset);
            Tick();
            _run.CurrentChoice.Choose(0);

            Assert.IsTrue(DialogueMemory.WasChosen(_target, _a.Id));
            Assert.IsFalse(DialogueMemory.WasChosen(_asset, _a.Id));
        }

        [Test]
        public void Visible_False_Hidden() {
            ChoiceOption _hidden = Option(_visible: false);
            ChoiceOption _shown = Option();
            DialogueAsset _asset = WithChoice(out _, _hidden, _shown);

            DialogueRun _run = Dialogue.Start(_asset);
            Tick();

            CollectionAssert.AreEqual(new[] { _shown }, Options(_run.CurrentChoice));
            Assert.IsTrue(_run.CurrentChoice.Options[0].Available);
        }

        [Test]
        public void Available_False_ListedButNotChoosable() {
            ChoiceOption _locked = Option(_available: false);
            ChoiceOption _open = Option();
            DialogueAsset _asset = WithChoice(out _, _locked, _open);

            DialogueRun _run = Dialogue.Start(_asset);
            Tick();
            ChoicePrompt _prompt = _run.CurrentChoice;

            CollectionAssert.AreEqual(new[] { _locked, _open }, Options(_prompt));
            Assert.IsFalse(_prompt.Options[0].Available);
            Assert.IsTrue(_prompt.Options[1].Available);

            _prompt.Select(0);
            Assert.AreEqual(-1, _prompt.SelectedIndex);
            _prompt.Choose(0);
            _prompt.Confirm();
            Tick();
            Assert.AreSame(_prompt, _run.CurrentChoice);
            CollectionAssert.IsEmpty(made);
            Assert.IsFalse(DialogueMemory.WasChosen(_asset, _locked.Id));

            _prompt.Select(1);
            Assert.AreEqual(1, _prompt.SelectedIndex);
            _prompt.Confirm();
            CollectionAssert.AreEqual(new[] { _open }, made);
        }

        [Test]
        public void AfterChosen_MarkSeen_Disable_Hide() {
            ChoiceOption _seen = Option(_after: AfterChosen.MarkSeen);
            ChoiceOption _disable = Option(_after: AfterChosen.Disable);
            ChoiceOption _hide = Option(_after: AfterChosen.Hide);
            ChoiceOption _nothing = Option();
            DialogueAsset _asset = WithChoice(out ChoiceNode _choice, _seen, _disable, _hide, _nothing);
            // Every option leads to a line that loops back to the choice
            foreach (ChoiceOption _option in _choice.Options) {
                LineNode _line = AddLine(_asset, "back");
                Link(_option.Next, _line);
                Link(_line.Next, _choice);
            }

            DialogueRun _run = Dialogue.Start(_asset);
            Tick();
            ChoicePrompt _prompt = _run.CurrentChoice;
            Assert.AreEqual(4, _prompt.Options.Count);
            foreach (ChoiceOptionState _state in _prompt.Options) {
                Assert.IsTrue(_state.Available);
                Assert.IsFalse(_state.Seen);
            }

            ChooseAndLoop(_run, _seen);
            ChooseAndLoop(_run, _disable);
            ChooseAndLoop(_run, _hide);
            ChooseAndLoop(_run, _nothing);
            AssertAfterChosen(_run.CurrentChoice, _seen, _disable, _hide, _nothing);

            // The state comes from the memory, so a new run sees it too
            _run.Stop();
            DialogueRun _again = Dialogue.Start(_asset);
            Tick();
            AssertAfterChosen(_again.CurrentChoice, _seen, _disable, _hide, _nothing);
        }

        private void ChooseAndLoop(DialogueRun _run, ChoiceOption _option) {
            ChoicePrompt _prompt = _run.CurrentChoice;
            int _index = System.Array.IndexOf(Options(_prompt), _option);
            Assert.GreaterOrEqual(_index, 0);
            _prompt.Choose(_index);
            Tick();
            channel.Last.Advance();
            Tick();
            Assert.IsNotNull(_run.CurrentChoice);
            Assert.AreNotSame(_prompt, _run.CurrentChoice);
        }

        private static void AssertAfterChosen(ChoicePrompt _prompt, ChoiceOption _seen, ChoiceOption _disable, ChoiceOption _hide, ChoiceOption _nothing) {
            CollectionAssert.AreEqual(new[] { _seen, _disable, _nothing }, Options(_prompt));
            Assert.IsTrue(StateOf(_prompt, _seen).Seen);
            Assert.IsTrue(StateOf(_prompt, _seen).Available);
            Assert.IsFalse(StateOf(_prompt, _disable).Available);
            Assert.IsFalse(StateOf(_prompt, _disable).Seen);
            Assert.IsTrue(StateOf(_prompt, _nothing).Available);
            Assert.IsFalse(StateOf(_prompt, _nothing).Seen);
            Assert.IsNull(StateOf(_prompt, _hide));
        }
        #endregion

        // ==================== TIMER ====================
        #region Timer
        private static void SetTimer(ChoiceNode _choice, float _seconds, TimeoutRule _rule) {
            _choice.UseTimer = true;
            _choice.TimerSeconds = _seconds;
            _choice.TimeoutRule = _rule;
        }

        [Test]
        public void Timer_ChooseSelected_UsesSelection() {
            ChoiceOption _a = Option();
            ChoiceOption _b = Option(_isDefault: true);
            DialogueAsset _asset = WithChoice(out ChoiceNode _choice, _a, _b);
            SetTimer(_choice, 1f, TimeoutRule.ChooseSelected);

            DialogueRun _run = Dialogue.Start(_asset);
            Tick();
            ChoicePrompt _prompt = _run.CurrentChoice;
            Assert.IsTrue(_prompt.HasTimer);
            Assert.AreEqual(1f, _prompt.Remaining01, 0.0001f);
            Assert.IsTrue(_prompt.Options[1].IsDefault);
            _prompt.Select(0);

            Tick(0.5f);
            Assert.AreEqual(0.5f, _prompt.Remaining01, 0.0001f);
            CollectionAssert.IsEmpty(made);

            Tick(0.6f);
            Assert.AreEqual(0f, _prompt.Remaining01, 0.0001f);
            CollectionAssert.AreEqual(new[] { _a }, made);
            Assert.IsTrue(DialogueMemory.WasChosen(_asset, _a.Id));
            Assert.IsNull(_run.CurrentChoice);
        }

        [Test]
        public void Timer_ChooseSelected_NoSelection_UsesDefault_ThenFirstAvailable() {
            ChoiceOption _a = Option();
            ChoiceOption _b = Option(_isDefault: true);
            Assert.AreSame(_b, RunUntilTimeout(_a, _b));

            ChoiceOption _locked = Option(_available: false);
            ChoiceOption _first = Option();
            ChoiceOption _second = Option();
            Assert.AreSame(_first, RunUntilTimeout(_locked, _first, _second));

            ChoiceOption _lockedDefault = Option(_available: false, _isDefault: true);
            ChoiceOption _open = Option();
            Assert.AreSame(_open, RunUntilTimeout(_lockedDefault, _open));
        }

        private ChoiceOption RunUntilTimeout(params ChoiceOption[] _options) {
            made.Clear();
            DialogueAsset _asset = WithChoice(out ChoiceNode _choice, _options);
            SetTimer(_choice, 1f, TimeoutRule.ChooseSelected);
            DialogueRun _run = Dialogue.Start(_asset);
            Tick();
            Tick(1.1f);
            Assert.AreEqual(RunState.Ended, _run.State);
            Assert.AreEqual(1, made.Count);
            return made[0];
        }

        [Test]
        public void Timer_NoAnswer_TakesNoAnswerOutput() {
            ChoiceOption _a = Option(_isDefault: true);
            DialogueAsset _asset = WithChoice(out ChoiceNode _choice, _a);
            LineNode _none = AddLine(_asset, "none");
            Link(_choice.NoAnswer, _none);
            SetTimer(_choice, 1f, TimeoutRule.NoAnswer);

            DialogueRun _run = Dialogue.Start(_asset);
            Tick();
            ChoicePrompt _prompt = _run.CurrentChoice;
            _prompt.Select(0);
            Tick(1.1f);

            Assert.IsNull(_run.CurrentChoice);
            CollectionAssert.IsEmpty(made);
            Assert.IsFalse(DialogueMemory.WasChosen(_asset, _a.Id));
            Assert.AreSame(_none, _run.Current);
            Assert.IsTrue(RecordingAction.Log.Contains("present:none"));
        }

        [Test]
        public void Timer_RespectsPause() {
            ChoiceOption _a = Option();
            DialogueAsset _asset = WithChoice(out ChoiceNode _choice, _a);
            SetTimer(_choice, 1f, TimeoutRule.ChooseSelected);

            DialogueRun _run = Dialogue.Start(_asset);
            Tick();
            ChoicePrompt _prompt = _run.CurrentChoice;
            Tick(0.5f);

            _run.Pause();
            Assert.AreEqual(RunState.Paused, _run.State);
            Tick(5f);
            Assert.AreEqual(0.5f, _prompt.Remaining01, 0.0001f);
            CollectionAssert.IsEmpty(made);

            _run.Resume();
            Assert.AreEqual(RunState.WaitingChoice, _run.State);
            Tick(0.4f);
            CollectionAssert.IsEmpty(made);
            Tick(0.2f);
            CollectionAssert.AreEqual(new[] { _a }, made);
        }

        [Test]
        public void Timer_UsesRunTimeMode() {
            ChoiceOption _a = Option();
            DialogueAsset _asset = WithChoice(out ChoiceNode _choice, _a);
            SetTimer(_choice, 1f, TimeoutRule.ChooseSelected);

            DialogueRun _run = Dialogue.Start(_asset, new DialogueStartOptions { Time = DialogueTime.Scaled });
            Dialogue.TickAll(0f, 0f);
            Dialogue.TickAll(0f, 5f);
            CollectionAssert.IsEmpty(made);
            Dialogue.TickAll(1.1f, 0f);
            CollectionAssert.AreEqual(new[] { _a }, made);
        }
        #endregion

        // ==================== NO OPTIONS ====================
        #region No Options
        [Test]
        public void AllOptionsHidden_TakesNoAnswerOrEnds() {
            // Connected: the second pass takes the no answer output
            DialogueAsset _asset = HiddenAfterChosenLoop(out ChoiceNode _choice, out ChoiceOption _only);
            LineNode _none = AddLine(_asset, "none");
            Link(_choice.NoAnswer, _none);

            DialogueRun _run = Dialogue.Start(_asset);
            PassOnceThenLoopBack(_run);
            Assert.IsNull(_run.CurrentChoice);
            Assert.AreSame(_none, _run.Current);
            Assert.AreEqual(1, presented.Count);

            channel.Last.Advance();
            Tick();
            Assert.AreEqual(RunState.Ended, _run.State);
            CollectionAssert.AreEqual(new[] { EndReason.Completed }, endReasons);
            LogAssert.NoUnexpectedReceived();

            // Not connected: the second pass ends the dialogue with a warning
            presented.Clear();
            endReasons.Clear();
            DialogueMemory.ClearForTests();
            DialogueAsset _ends = HiddenAfterChosenLoop(out _, out _);
            DialogueRun _endsRun = Dialogue.Start(_ends);
            LogAssert.Expect(LogType.Warning, new Regex("no option"));
            PassOnceThenLoopBack(_endsRun);
            Assert.AreEqual(RunState.Ended, _endsRun.State);
            Assert.IsNull(_endsRun.CurrentChoice);
            Assert.AreEqual(1, presented.Count);
            CollectionAssert.AreEqual(new[] { EndReason.Completed }, endReasons);
        }

        /// <summary> Start → choice with one option hidden once chosen → line → back to the choice </summary>
        private DialogueAsset HiddenAfterChosenLoop(out ChoiceNode _choice, out ChoiceOption _only) {
            _only = Option(_after: AfterChosen.Hide);
            DialogueAsset _asset = WithChoice(out _choice, _only);
            LineNode _line = AddLine(_asset, "loop");
            Link(_only.Next, _line);
            Link(_line.Next, _choice);
            return _asset;
        }

        private void PassOnceThenLoopBack(DialogueRun _run) {
            Tick();
            _run.CurrentChoice.Choose(0);
            Tick();
            channel.Last.Advance();
            Tick();
        }
        #endregion

        // ==================== AMBIENT ====================
        #region Ambient
        [Test]
        public void Ambient_ChoosesImmediately_Warns() {
            ChoiceOption _a = Option();
            ChoiceOption _b = Option(_isDefault: true);
            DialogueAsset _asset = WithChoice(out ChoiceNode _choice, _a, _b);
            LineNode _line = AddLine(_asset, "b");
            Link(_b.Next, _line);
            Link(_line.Next, _choice);

            DialogueRun _run = Dialogue.Start(_asset, new DialogueStartOptions { IsMain = false });
            LogAssert.Expect(LogType.Warning, new Regex("ambient"));
            Tick();
            Assert.IsNull(_run.CurrentChoice);
            Assert.AreSame(_line, _run.Current);
            CollectionAssert.IsEmpty(presented);
            CollectionAssert.AreEqual(new[] { _b }, made);
            Assert.IsTrue(DialogueMemory.WasChosen(_asset, _b.Id));

            // Second pass through the choice: chosen again, no second warning
            channel.Last.Advance();
            Tick();
            CollectionAssert.AreEqual(new[] { _b, _b }, made);
            LogAssert.NoUnexpectedReceived();

            // Nothing available: the no answer output
            ChoiceOption _locked = Option(_available: false);
            DialogueAsset _blocked = WithChoice(out ChoiceNode _blockedChoice, _locked);
            LineNode _none = AddLine(_blocked, "none");
            Link(_blockedChoice.NoAnswer, _none);
            DialogueRun _blockedRun = Dialogue.Start(_blocked, new DialogueStartOptions { IsMain = false });
            LogAssert.Expect(LogType.Warning, new Regex("ambient"));
            Tick();
            Assert.AreSame(_none, _blockedRun.Current);
            CollectionAssert.AreEqual(new[] { _b, _b }, made);
        }
        #endregion

        // ==================== SELECTION ====================
        #region Selection
        [Test]
        public void MoveSelection_SkipsUnavailable_Wraps() {
            ChoiceOption _a = Option();
            ChoiceOption _locked = Option(_available: false);
            ChoiceOption _c = Option();
            DialogueAsset _asset = WithChoice(out _, _a, _locked, _c);

            DialogueRun _run = Dialogue.Start(_asset);
            Tick();
            ChoicePrompt _prompt = _run.CurrentChoice;
            int _changed = 0;
            _prompt.Changed += () => _changed++;

            _prompt.MoveSelection(1);
            Assert.AreEqual(0, _prompt.SelectedIndex);
            _prompt.MoveSelection(1);
            Assert.AreEqual(2, _prompt.SelectedIndex);
            _prompt.MoveSelection(1);
            Assert.AreEqual(0, _prompt.SelectedIndex);
            _prompt.MoveSelection(-1);
            Assert.AreEqual(2, _prompt.SelectedIndex);
            Assert.AreEqual(4, _changed);

            _prompt.Select(-1);
            Assert.AreEqual(-1, _prompt.SelectedIndex);
            _prompt.MoveSelection(-1);
            Assert.AreEqual(2, _prompt.SelectedIndex);
            Assert.AreEqual(6, _changed);

            _prompt.Select(2);
            Assert.AreEqual(6, _changed);

            _prompt.Confirm();
            CollectionAssert.AreEqual(new[] { _c }, made);
        }
        #endregion
    }
}
