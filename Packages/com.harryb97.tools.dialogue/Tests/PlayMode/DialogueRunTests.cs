using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using HarryTools.Actions;
using HarryTools.Conditions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarryTools.Dialogue.Tests {
    public class DialogueRunTests {
        // ==================== SETUP ====================
        #region Setup
        private readonly List<DialogueAsset> assets = new List<DialogueAsset>();
        private readonly List<EndReason> endReasons = new List<EndReason>();
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
            Dialogue.Ended += OnEnded;
        }

        [TearDown]
        public void TearDown() {
            Dialogue.Ended -= OnEnded;
            Dialogue.StopAll();
            Dialogue.ClearForTests();
            DialogueSettings.ClearForTests();
            DialogueMemory.ClearForTests();
            Time.timeScale = 1f;
            for (int _i = 0; _i < assets.Count; _i++) Object.DestroyImmediate(assets[_i]);
            assets.Clear();
            Object.DestroyImmediate(settings);
        }

        private void OnEnded(DialogueRun _run, EndReason _reason) {
            endReasons.Add(_reason);
        }
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

        private static void Link(NodeOutput _output, DialogueNode _to) {
            _output.Target = _to.Id;
        }

        /// <summary> Start → one line, nothing after </summary>
        private DialogueAsset SingleLine(string _label, out LineNode _line) {
            DialogueAsset _asset = NewAsset();
            _line = AddLine(_asset, _label);
            Link(_asset.Start.Next, _line);
            return _asset;
        }

        private static void Tick(float _dt = 0f) => Dialogue.TickAll(_dt, _dt);

        private static void LogLines(DialogueRun _run) {
            _run.LineStarted += (_, _line) => RecordingAction.Log.Add("started:" + _line.WriterNote);
            _run.LineEnded += (_, _line) => RecordingAction.Log.Add("ended:" + _line.WriterNote);
        }

        private static IEnumerator TickFramesUntil(System.Func<bool> _done, int _maxFrames = 30) {
            for (int _i = 0; _i < _maxFrames && !_done(); _i++) {
                yield return null;
                Tick(0.016f);
            }
        }
        #endregion

        // ==================== START ====================
        #region Start
        [Test]
        public void Start_MarksPlayed_FiresStarted() {
            DialogueAsset _asset = SingleLine("a", out _);
            DialogueRun _started = null;
            System.Action<DialogueRun> _onStarted = _run => _started = _run;
            Dialogue.Started += _onStarted;

            DialogueRun _run = Dialogue.Start(_asset);
            Dialogue.Started -= _onStarted;

            Assert.IsNotNull(_run);
            Assert.AreSame(_run, _started);
            Assert.AreEqual(1, DialogueMemory.PlayCount(_asset));
            Assert.IsTrue(_run.IsMain);
            Assert.AreSame(_run, Dialogue.Main);
            CollectionAssert.Contains(Dialogue.Active, _run);
            Assert.AreEqual(RunState.Running, _run.State);
            Assert.AreSame(_asset, _run.Asset);
        }

        [Test]
        public void Start_MainWhileMain_ReturnsNullAndWarns() {
            DialogueAsset _asset = SingleLine("a", out _);
            DialogueRun _first = Dialogue.Start(_asset);

            LogAssert.Expect(LogType.Warning, new Regex("Dialogue"));
            DialogueRun _second = Dialogue.Start(_asset);
            DialogueRun _ambient = Dialogue.Start(_asset, new DialogueStartOptions { IsMain = false });

            Assert.IsNull(_second);
            Assert.AreSame(_first, Dialogue.Main);
            Assert.IsNotNull(_ambient);
            Assert.IsFalse(_ambient.IsMain);
            Assert.AreEqual(2, Dialogue.Active.Count);
        }
        #endregion

        // ==================== LINES ====================
        #region Lines
        [Test]
        public void Line_Order_DelayBefore_Before_Present_OnStart_OnEnd_DelayAfter() {
            DialogueAsset _asset = NewAsset();
            LineNode _a = AddLine(_asset, "a");
            LineNode _b = AddLine(_asset, "b");
            Link(_asset.Start.Next, _a);
            Link(_a.Next, _b);
            _a.DelayBefore = 1f;
            _a.DelayAfter = 1f;
            _a.Before.Actions = new ActionList(new RecordingAction("before"));
            _a.OnStart.Actions = new ActionList(new RecordingAction("onStart"));
            _a.OnEnd.Actions = new ActionList(new RecordingAction("onEnd"));

            DialogueRun _run = Dialogue.Start(_asset);
            LogLines(_run);
            Tick();
            Tick(0.5f);
            CollectionAssert.IsEmpty(RecordingAction.Log);

            Tick(0.6f);
            CollectionAssert.AreEqual(new[] { "before", "before:done", "present:a", "started:a", "onStart", "onStart:done" }, RecordingAction.Log);

            RecordingAction.Log.Clear();
            channel.Last.Advance();
            Tick();
            CollectionAssert.AreEqual(new[] { "ended:a", "onEnd", "onEnd:done" }, RecordingAction.Log);
            Assert.IsTrue(channel.Last.Released);

            RecordingAction.Log.Clear();
            Tick(0.5f);
            CollectionAssert.IsEmpty(RecordingAction.Log);
            Tick(0.6f);
            CollectionAssert.AreEqual(new[] { "present:b", "started:b" }, RecordingAction.Log);
        }

        [UnityTest]
        public IEnumerator Line_BeforeActions_Awaited_OnStart_NotAwaited() {
            DialogueAsset _asset = NewAsset();
            LineNode _a = AddLine(_asset, "a");
            LineNode _b = AddLine(_asset, "b");
            Link(_asset.Start.Next, _a);
            Link(_a.Next, _b);
            _a.Before.Actions = new ActionList(new RecordingAction("before", 3));
            _a.OnStart.Actions = new ActionList(new RecordingAction("onStart", 5));

            Dialogue.Start(_asset);
            Tick();
            CollectionAssert.AreEqual(new[] { "before" }, RecordingAction.Log);

            yield return TickFramesUntil(() => RecordingAction.Log.Contains("present:a"));
            List<string> _log = RecordingAction.Log;
            Assert.Less(_log.IndexOf("before:done"), _log.IndexOf("present:a"));
            Assert.IsTrue(_log.Contains("onStart"));
            Assert.IsFalse(_log.Contains("onStart:done"));

            channel.Last.Advance();
            Tick();
            Assert.IsTrue(_log.Contains("present:b"));
            Assert.IsFalse(_log.Contains("onStart:done"));

            yield return TickFramesUntil(() => _log.Contains("onStart:done"));
            Assert.IsTrue(_log.Contains("onStart:done"));
        }

        [Test]
        public void Headless_NoChannel_UsesLifetime() {
            Dialogue.ChannelResolver = null;
            DialogueAsset _asset = NewAsset();
            LineNode _fixed = AddLine(_asset, "fixed");
            LineNode _length = AddLine(_asset, "length");
            LineNode _untilNext = AddLine(_asset, "untilNext");
            Link(_asset.Start.Next, _fixed);
            Link(_fixed.Next, _length);
            Link(_length.Next, _untilNext);

            LineLifetime _lifetime = LineLifetime.Default;
            _lifetime.Mode = LineLifetimeMode.Fixed;
            _lifetime.Seconds = 1f;
            _fixed.OverrideLifetime = true;
            _fixed.Lifetime = _lifetime;
            _fixed.OverrideAdvance = true;
            _fixed.Advance = AdvanceMode.Auto;

            // Empty text: the default by-length lifetime is only its base
            _lifetime = LineLifetime.Default;
            _length.OverrideLifetime = true;
            _length.Lifetime = _lifetime;

            _lifetime.Mode = LineLifetimeMode.UntilNext;
            _untilNext.OverrideLifetime = true;
            _untilNext.Lifetime = _lifetime;

            DialogueRun _run = Dialogue.Start(_asset);
            LogLines(_run);
            Tick();
            Assert.AreSame(_fixed, _run.Current);
            Tick(0.5f);
            Assert.IsFalse(RecordingAction.Log.Contains("ended:fixed"));
            Tick(0.6f);
            Assert.IsTrue(RecordingAction.Log.Contains("ended:fixed"));
            Assert.AreSame(_length, _run.Current);

            Tick(LineLifetime.Default.Base - 0.1f);
            Assert.AreSame(_length, _run.Current);
            Tick(0.2f);
            Assert.AreSame(_untilNext, _run.Current);

            Tick(1000f);
            Assert.AreSame(_untilNext, _run.Current);
            _run.Advance();
            Tick();
            Assert.AreEqual(RunState.Ended, _run.State);
            CollectionAssert.AreEqual(new[] { EndReason.Completed }, endReasons);
        }
        #endregion

        // ==================== NODES ====================
        #region Nodes
        [UnityTest]
        public IEnumerator ActionsNode_Wait_And_NoWait() {
            DialogueAsset _asset = NewAsset();
            ActionsNode _wait = _asset.AddNode<ActionsNode>();
            ActionsNode _noWait = _asset.AddNode<ActionsNode>();
            LineNode _line = AddLine(_asset, "line");
            Link(_asset.Start.Next, _wait);
            Link(_wait.Next, _noWait);
            Link(_noWait.Next, _line);
            _wait.Actions = new ActionList(new RecordingAction("wait", 2));
            _noWait.Actions = new ActionList(new RecordingAction("noWait", 3));
            _noWait.WaitForCompletion = false;

            Dialogue.Start(_asset);
            Tick();
            CollectionAssert.AreEqual(new[] { "wait" }, RecordingAction.Log);

            yield return TickFramesUntil(() => RecordingAction.Log.Contains("present:line"));
            List<string> _log = RecordingAction.Log;
            Assert.Less(_log.IndexOf("wait:done"), _log.IndexOf("noWait"));
            Assert.IsTrue(_log.Contains("present:line"));
            Assert.IsFalse(_log.Contains("noWait:done"));
        }

        [Test]
        public void Branch_FirstMatch_ThenOtherwise() {
            Assert.AreEqual("B", RunBranch(false, true, true));
            Assert.AreEqual("O", RunBranch(false));
            Assert.AreEqual("A", RunBranch(null, false));
        }

        /// <summary> Builds a branch whose cases lead to lines A, B, C... and whose otherwise leads to O. A null case has an empty condition list </summary>
        private string RunBranch(params bool?[] _cases) {
            DialogueAsset _asset = NewAsset();
            BranchNode _branch = _asset.AddNode<BranchNode>();
            Link(_asset.Start.Next, _branch);
            for (int _i = 0; _i < _cases.Length; _i++) {
                BranchCase _case = new BranchCase();
                _case.Conditions = _cases[_i].HasValue
                    ? new ConditionList(ConditionList.EvaluationMode.All, new FixedCondition(_cases[_i].Value))
                    : new ConditionList();
                Link(_case.Next, AddLine(_asset, ((char)('A' + _i)).ToString()));
                _branch.Cases.Add(_case);
            }
            Link(_branch.Otherwise, AddLine(_asset, "O"));

            DialogueRun _run = Dialogue.Start(_asset);
            Tick();
            string _label = ((LineNode)_run.Current).WriterNote;
            _run.Stop();
            return _label;
        }

        [Test]
        public void Jump_Continue_RunsTarget() {
            DialogueAsset _target = SingleLine("target", out LineNode _targetLine);
            DialogueAsset _asset = NewAsset();
            JumpNode _jump = _asset.AddNode<JumpNode>();
            _jump.Target = _target;
            _jump.Mode = JumpMode.Continue;
            Link(_asset.Start.Next, _jump);
            Link(_jump.Next, AddLine(_asset, "after"));

            DialogueRun _run = Dialogue.Start(_asset);
            Tick();
            Assert.AreSame(_targetLine, _run.Current);
            Assert.AreSame(_target, _run.CurrentAsset);
            Assert.AreSame(_asset, _run.Asset);

            channel.Last.Advance();
            Tick();
            Assert.AreEqual(RunState.Ended, _run.State);
            Assert.IsFalse(RecordingAction.Log.Contains("present:after"));
            CollectionAssert.AreEqual(new[] { EndReason.Completed }, endReasons);
        }

        [Test]
        public void Jump_CallAndReturn_ReturnsToNext() {
            DialogueAsset _target = NewAsset();
            LineNode _targetLine = AddLine(_target, "target");
            EndNode _end = _target.AddNode<EndNode>();
            Link(_target.Start.Next, _targetLine);
            Link(_targetLine.Next, _end);

            DialogueAsset _asset = NewAsset();
            JumpNode _jump = _asset.AddNode<JumpNode>();
            _jump.Target = _target;
            _jump.Mode = JumpMode.CallAndReturn;
            LineNode _after = AddLine(_asset, "after");
            Link(_asset.Start.Next, _jump);
            Link(_jump.Next, _after);

            DialogueRun _run = Dialogue.Start(_asset);
            Tick();
            Assert.AreSame(_targetLine, _run.Current);

            channel.Last.Advance();
            Tick();
            Assert.AreSame(_after, _run.Current);
            Assert.AreSame(_asset, _run.CurrentAsset);

            channel.Last.Advance();
            Tick();
            Assert.AreEqual(RunState.Ended, _run.State);
            CollectionAssert.AreEqual(new[] { EndReason.Completed }, endReasons);
        }

        [Test]
        public void UnconnectedOutput_Ends_Completed() {
            DialogueAsset _asset = SingleLine("a", out _);
            DialogueRun _run = Dialogue.Start(_asset);
            EndReason? _runReason = null;
            _run.Ended += (_, _reason) => _runReason = _reason;
            Tick();

            channel.Last.Advance();
            Tick();

            Assert.AreEqual(RunState.Ended, _run.State);
            Assert.AreEqual(EndReason.Completed, _runReason);
            CollectionAssert.AreEqual(new[] { EndReason.Completed }, endReasons);
            Assert.IsTrue(channel.Last.Released);
            Assert.IsNull(Dialogue.Main);
            CollectionAssert.IsEmpty(Dialogue.Active);
        }
        #endregion

        // ==================== CONTROL ====================
        #region Control
        [Test]
        public void TickAll_ExceptionInOneRun_OthersContinue() {
            DialogueRun _broken = Dialogue.Start(SingleLine("broken", out _), new DialogueStartOptions { IsMain = false });
            DialogueRun _healthy = Dialogue.Start(SingleLine("healthy", out _), new DialogueStartOptions { IsMain = false });
            Dialogue.ChannelResolver = _line => {
                if (_line.Run == _broken) throw new System.InvalidOperationException("channel failure");
                return channel;
            };

            LogAssert.Expect(LogType.Error, new Regex("channel failure"));
            Tick();

            Assert.AreEqual(RunState.Ended, _broken.State);
            CollectionAssert.AreEqual(new[] { EndReason.Stopped }, endReasons);
            Assert.AreEqual(RunState.Running, _healthy.State);
            CollectionAssert.AreEqual(new[] { "present:healthy" }, RecordingAction.Log);

            channel.Last.Advance();
            Tick();
            Assert.AreEqual(RunState.Ended, _healthy.State);
            CollectionAssert.AreEqual(new[] { EndReason.Stopped, EndReason.Completed }, endReasons);
        }

        [Test]
        public void Stop_EndsWithStopped_ReleasesLine() {
            DialogueAsset _asset = SingleLine("a", out _);
            DialogueRun _run = Dialogue.Start(_asset);
            LogLines(_run);
            Tick();

            _run.Stop();
            _run.Stop();
            Tick();

            Assert.AreEqual(RunState.Ended, _run.State);
            Assert.IsTrue(channel.Last.Released);
            CollectionAssert.AreEqual(new[] { EndReason.Stopped }, endReasons);
            Assert.IsTrue(RecordingAction.Log.Contains("ended:a"));
            Assert.IsNull(Dialogue.Main);
        }

        [Test]
        public void StopFromInsideLineAction_EndsOnce() {
            DialogueAsset _asset = NewAsset();
            LineNode _a = AddLine(_asset, "a");
            LineNode _b = AddLine(_asset, "b");
            Link(_asset.Start.Next, _a);
            Link(_a.Next, _b);
            DialogueRun _run = null;
            _a.OnStart.Actions = new ActionList(
                new CallbackAction(() => _run.Stop()),
                new CallbackAction(Dialogue.StopAll),
                new RecordingAction("afterStop"));

            _run = Dialogue.Start(_asset);
            int _runEnded = 0;
            _run.Ended += (_, _) => {
                _runEnded++;
                _run.Stop();
            };
            Tick();
            Tick(1f);

            Assert.AreEqual(RunState.Ended, _run.State);
            Assert.AreEqual(1, _runEnded);
            CollectionAssert.AreEqual(new[] { EndReason.Stopped }, endReasons);
            Assert.IsTrue(channel.Last.Released);
            Assert.IsFalse(RecordingAction.Log.Contains("present:b"));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Pause_StopsTicking_Resume_Continues() {
            DialogueAsset _asset = SingleLine("a", out LineNode _line);
            _line.DelayBefore = 1f;
            DialogueRun _run = Dialogue.Start(_asset);
            Tick();

            _run.Pause();
            Assert.AreEqual(RunState.Paused, _run.State);
            Tick(5f);
            CollectionAssert.IsEmpty(RecordingAction.Log);

            _run.Resume();
            Assert.AreEqual(RunState.Running, _run.State);
            Tick(0.5f);
            CollectionAssert.IsEmpty(RecordingAction.Log);
            Tick(0.6f);
            CollectionAssert.AreEqual(new[] { "present:a" }, RecordingAction.Log);

            Dialogue.PauseAll();
            Assert.IsTrue(channel.Last.Paused);
            channel.Last.Advance();
            Tick();
            Assert.AreEqual(RunState.Paused, _run.State);
            Dialogue.ResumeAll();
            Assert.IsFalse(channel.Last.Paused);
            Tick();
            Assert.AreEqual(RunState.Ended, _run.State);
        }
        #endregion

        // ==================== TIME ====================
        #region Time
        [UnityTest]
        public IEnumerator Time_Unscaled_IgnoresTimeScale() {
            DialogueAsset _unscaled = SingleLine("unscaled", out LineNode _unscaledLine);
            DialogueAsset _scaled = SingleLine("scaled", out LineNode _scaledLine);
            _unscaledLine.DelayBefore = 0.05f;
            _scaledLine.DelayBefore = 0.05f;

            Dialogue.ManualTick = false;
            Time.timeScale = 0f;
            Dialogue.Start(_unscaled, new DialogueStartOptions { Time = DialogueTime.Unscaled });
            Dialogue.Start(_scaled, new DialogueStartOptions { IsMain = false, Time = DialogueTime.Scaled });

            float _until = Time.realtimeSinceStartup + 2f;
            while (!RecordingAction.Log.Contains("present:unscaled") && Time.realtimeSinceStartup < _until) yield return null;

            Assert.IsTrue(RecordingAction.Log.Contains("present:unscaled"));
            Assert.IsFalse(RecordingAction.Log.Contains("present:scaled"));
        }

        [Test]
        public void Time_PerDialogueOverride() {
            DialogueAsset _asset = SingleLine("a", out LineNode _line);
            _line.DelayBefore = 1f;
            _asset.OverrideTime = true;
            _asset.Time = DialogueTime.Scaled;

            DialogueRun _run = Dialogue.Start(_asset);
            Assert.AreEqual(DialogueTime.Scaled, _run.Time);
            Dialogue.TickAll(0f, 0f);
            Dialogue.TickAll(0f, 5f);
            CollectionAssert.IsEmpty(RecordingAction.Log);
            Dialogue.TickAll(1.1f, 0f);
            CollectionAssert.AreEqual(new[] { "present:a" }, RecordingAction.Log);
            _run.Stop();

            DialogueRun _call = Dialogue.Start(_asset, new DialogueStartOptions { Time = DialogueTime.Unscaled });
            Assert.AreEqual(DialogueTime.Unscaled, _call.Time);
        }
        #endregion
    }
}
