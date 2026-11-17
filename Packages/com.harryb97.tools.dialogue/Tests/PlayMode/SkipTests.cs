using System.Collections;
using System.Collections.Generic;
using HarryTools.Actions;
using HarryTools.TextEffects;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarryTools.Dialogue.Tests {
    public class SkipTests {
        // ==================== SETUP ====================
        #region Setup
        private readonly List<DialogueAsset> assets = new List<DialogueAsset>();
        private readonly List<EndReason> endReasons = new List<EndReason>();
        private readonly Dictionary<LineNode, string> texts = new Dictionary<LineNode, string>();
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
            texts.Clear();
            Dialogue.LineTextResolver = _line => texts.TryGetValue(_line, out string _text) ? _text : null;
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
            for (int _i = 0; _i < assets.Count; _i++) Object.DestroyImmediate(assets[_i]);
            assets.Clear();
            Object.DestroyImmediate(settings);
        }

        private void OnEnded(DialogueRun _run, EndReason _reason) => endReasons.Add(_reason);
        #endregion

        // ==================== HELPERS ====================
        #region Helpers
        private DialogueAsset NewAsset() {
            DialogueAsset _asset = ScriptableObject.CreateInstance<DialogueAsset>();
            assets.Add(_asset);
            return _asset;
        }

        /// <summary> A line whose before, on start and on end actions record "{label}Before", "{label}Start" and "{label}End" </summary>
        private static LineNode AddLine(DialogueAsset _asset, string _label, int _beforeFrames = 0) {
            LineNode _line = _asset.AddNode<LineNode>();
            _line.WriterNote = _label;
            _line.Before.Actions = new ActionList(new RecordingAction(_label + "Before", _beforeFrames));
            _line.OnStart.Actions = new ActionList(new RecordingAction(_label + "Start"));
            _line.OnEnd.Actions = new ActionList(new RecordingAction(_label + "End"));
            return _line;
        }

        private static ActionsNode AddActions(DialogueAsset _asset, string _tag, int _frames = 0) {
            ActionsNode _node = _asset.AddNode<ActionsNode>();
            _node.Actions = new ActionList(new RecordingAction(_tag, _frames));
            return _node;
        }

        private static ChoiceNode AddChoice(DialogueAsset _asset, params ChoiceOption[] _options) {
            ChoiceNode _choice = _asset.AddNode<ChoiceNode>();
            _choice.Options.AddRange(_options);
            _asset.EnsureIds();
            return _choice;
        }

        private static void Link(NodeOutput _output, DialogueNode _to) {
            _output.Target = _to.Id;
        }

        private static TextEventTable Events(params TextEventEntry[] _entries) {
            return new TextEventTable(new List<TextEventEntry>(_entries));
        }

        private static TextEventEntry Event(string _name, bool _runWhenSkipped = true) {
            return new TextEventEntry(_name, new ActionList(new RecordingAction("event:" + _name)), _runWhenSkipped);
        }

        private static void Tick(float _dt = 0f) => Dialogue.TickAll(_dt, _dt);

        private static IEnumerator FramesUntil(System.Func<bool> _done, int _maxFrames = 30) {
            for (int _i = 0; _i < _maxFrames && !_done(); _i++) yield return null;
        }
        #endregion

        // ==================== TESTS ====================
        #region Tests
        [UnityTest]
        public IEnumerator Skip_RunsActionsInOrder_StopsAtChoice() {
            DialogueAsset _asset = NewAsset();
            LineNode _a = AddLine(_asset, "a");
            ActionsNode _node = AddActions(_asset, "node");
            LineNode _b = AddLine(_asset, "b");
            ChoiceOption _option = new ChoiceOption();
            ChoiceNode _choice = AddChoice(_asset, _option);
            LineNode _c = AddLine(_asset, "c");
            Link(_asset.Start.Next, _a);
            Link(_a.Next, _node);
            Link(_node.Next, _b);
            Link(_b.Next, _choice);
            Link(_option.Next, _c);
            _b.DelayBefore = 10f;
            _b.DelayAfter = 10f;

            DialogueRun _run = Dialogue.Start(_asset);
            List<string> _ended = new List<string>();
            _run.LineEnded += (_, _line) => _ended.Add(_line.WriterNote);
            Tick();
            Assert.AreSame(_a, _run.Current);
            FakeLineHandle _shown = channel.Last;
            RecordingAction.Log.Clear();

            _run.Skip();
            Assert.IsTrue(_shown.Skipped);
            Assert.IsTrue(_shown.Released);
            CollectionAssert.AreEqual(new[] { "a" }, _ended);
            Assert.AreSame(_choice, _run.Current);
            Assert.AreEqual(RunState.WaitingChoice, _run.State);
            Assert.IsNotNull(_run.CurrentChoice);
            Assert.IsFalse(_run.IsSkipping);

            yield return FramesUntil(() => RecordingAction.Log.Contains("bEnd:done"));
            CollectionAssert.AreEqual(new[] {
                "aEnd", "aEnd:done", "node", "node:done",
                "bBefore", "bBefore:done", "bStart", "bStart:done", "bEnd", "bEnd:done"
            }, RecordingAction.Log);
            CollectionAssert.AreEqual(new[] { "a" }, _ended);

            // After the choice the run goes on normally
            RecordingAction.Log.Clear();
            _run.CurrentChoice.Choose(0);
            Tick();
            Assert.AreSame(_c, _run.Current);
            CollectionAssert.Contains(RecordingAction.Log, "present:c");
            Assert.IsFalse(channel.Last.Skipped);
        }

        [UnityTest]
        public IEnumerator Skip_RespectsRunWhenSkipped_False() {
            DialogueAsset _asset = NewAsset();
            LineNode _a = AddLine(_asset, "a");
            ActionsNode _node = AddActions(_asset, "node");
            LineNode _b = AddLine(_asset, "b");
            Link(_asset.Start.Next, _a);
            Link(_a.Next, _node);
            Link(_node.Next, _b);
            _a.OnEnd.RunWhenSkipped = false;
            _node.RunWhenSkipped = false;
            _b.Before.RunWhenSkipped = false;
            _b.OnEnd.RunWhenSkipped = false;
            _b.Events = Events(Event("kept"), Event("dropped", false));
            texts[_b] = "x<event=dropped>y<event=kept>z";

            DialogueRun _run = Dialogue.Start(_asset);
            Tick();
            RecordingAction.Log.Clear();

            _run.Skip();
            yield return FramesUntil(() => RecordingAction.Log.Contains("event:kept:done"));
            CollectionAssert.AreEqual(new[] { "bStart", "bStart:done", "event:kept", "event:kept:done" }, RecordingAction.Log);
            CollectionAssert.AreEqual(new[] { EndReason.Skipped }, endReasons);
        }

        [UnityTest]
        public IEnumerator Skip_TextEvents_FromParsedText_InOrder() {
            DialogueAsset _asset = NewAsset();
            LineNode _a = AddLine(_asset, "a");
            LineNode _b = AddLine(_asset, "b");
            Link(_asset.Start.Next, _a);
            Link(_a.Next, _b);
            _a.Events = Events(Event("first"));
            _b.Events = Events(Event("first"), Event("second"));
            // a has events in its table but no text: none run
            texts[_b] = "<event=second>one <event=unknown>two <event=first>three<event=second>";

            DialogueRun _run = Dialogue.Start(_asset);
            _run.Skip();
            Assert.AreEqual(RunState.Ended, _run.State);
            CollectionAssert.IsEmpty(channel.Handles);

            yield return FramesUntil(() => RecordingAction.Log.Contains("bEnd:done"));
            CollectionAssert.AreEqual(new[] {
                "aBefore", "aBefore:done", "aStart", "aStart:done", "aEnd", "aEnd:done",
                "bBefore", "bBefore:done", "bStart", "bStart:done",
                "event:second", "event:second:done", "event:first", "event:first:done", "event:second", "event:second:done",
                "bEnd", "bEnd:done"
            }, RecordingAction.Log);
        }

        [Test]
        public void Skip_ToEnd_EndReasonSkipped() {
            DialogueAsset _asset = NewAsset();
            LineNode _a = AddLine(_asset, "a");
            LineNode _b = AddLine(_asset, "b");
            Link(_asset.Start.Next, _a);
            Link(_a.Next, _b);
            _a.DelayAfter = 10f;
            _b.DelayBefore = 10f;

            DialogueRun _run = Dialogue.Start(_asset);
            List<EndReason> _runEnded = new List<EndReason>();
            _run.Ended += (_, _reason) => _runEnded.Add(_reason);
            Tick();
            _run.Skip();

            Assert.AreEqual(RunState.Ended, _run.State);
            Assert.IsFalse(_run.IsSkipping);
            CollectionAssert.AreEqual(new[] { EndReason.Skipped }, _runEnded);
            CollectionAssert.AreEqual(new[] { EndReason.Skipped }, endReasons);
            CollectionAssert.DoesNotContain(RecordingAction.Log, "present:b");
            Assert.IsNull(Dialogue.Main);
        }

        [Test]
        public void Skip_NotAllowed_Ignored() {
            DialogueAsset _asset = NewAsset();
            LineNode _a = AddLine(_asset, "a");
            LineNode _b = AddLine(_asset, "b");
            Link(_asset.Start.Next, _a);
            Link(_a.Next, _b);
            _asset.AllowSkip = false;

            DialogueRun _run = Dialogue.Start(_asset);
            Tick();
            Assert.IsFalse(_run.CanSkip);
            _run.Skip();
            Tick();

            Assert.IsFalse(_run.IsSkipping);
            Assert.IsFalse(channel.Last.Skipped);
            Assert.AreSame(_a, _run.Current);
            Assert.AreEqual(RunState.Running, _run.State);
            CollectionAssert.IsEmpty(endReasons);
        }

        [UnityTest]
        public IEnumerator Skip_DoesNotWaitForLongActions() {
            DialogueAsset _asset = NewAsset();
            LineNode _a = AddLine(_asset, "a", 5);
            ActionsNode _node = AddActions(_asset, "node", 5);
            LineNode _b = AddLine(_asset, "b", 5);
            Link(_asset.Start.Next, _a);
            Link(_a.Next, _node);
            Link(_node.Next, _b);

            DialogueRun _run = Dialogue.Start(_asset);
            Tick();
            CollectionAssert.AreEqual(new[] { "aBefore" }, RecordingAction.Log);

            _run.Skip();
            Assert.AreEqual(RunState.Ended, _run.State);
            CollectionAssert.AreEqual(new[] { EndReason.Skipped }, endReasons);
            CollectionAssert.IsEmpty(channel.Handles);
            CollectionAssert.DoesNotContain(RecordingAction.Log, "bEnd");

            // The queue keeps running after the run ended, one list after the other
            yield return FramesUntil(() => RecordingAction.Log.Contains("bEnd:done"), 60);
            List<string> _log = RecordingAction.Log;
            CollectionAssert.Contains(_log, "aBefore:done");
            Assert.Less(_log.IndexOf("node:done"), _log.IndexOf("bBefore"));
            Assert.Less(_log.IndexOf("bBefore:done"), _log.IndexOf("bStart"));
            Assert.Less(_log.IndexOf("bStart:done"), _log.IndexOf("bEnd"));
        }
        #endregion
    }
}
