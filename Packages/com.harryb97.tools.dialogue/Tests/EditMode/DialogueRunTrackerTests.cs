using System.Collections.Generic;
using HarryTools.Dialogue.Editor;
using NUnit.Framework;
using UnityEngine;

namespace HarryTools.Dialogue.Tests {
    public class DialogueRunTrackerTests {
        // ==================== SETUP ====================
        #region Setup
        private readonly List<DialogueAsset> assets = new List<DialogueAsset>();
        private DialogueSettings settings;
        private FakeLineChannel channel;
        private DialogueRunTracker tracker;

        [SetUp]
        public void SetUp() {
            // Runs left by a Play session would keep the main slot taken
            Dialogue.ClearForTests();
            DialogueMemory.ClearForTests();
            settings = ScriptableObject.CreateInstance<DialogueSettings>();
            DialogueSettings.UseForTests(settings);
            Dialogue.ManualTick = true;
            channel = new FakeLineChannel();
            Dialogue.ChannelResolver = _ => channel;
        }

        [TearDown]
        public void TearDown() {
            tracker?.Dispose();
            tracker = null;
            Dialogue.StopAll();
            Dialogue.ClearForTests();
            DialogueSettings.ClearForTests();
            DialogueMemory.ClearForTests();
            for (int _i = 0; _i < assets.Count; _i++) Object.DestroyImmediate(assets[_i]);
            assets.Clear();
            Object.DestroyImmediate(settings);
        }
        #endregion

        // ==================== HELPERS ====================
        #region Helpers
        private DialogueAsset NewAsset() {
            DialogueAsset _asset = ScriptableObject.CreateInstance<DialogueAsset>();
            assets.Add(_asset);
            return _asset;
        }

        private static LineNode AddLine(DialogueAsset _asset) {
            return _asset.AddNode<LineNode>();
        }

        private static void Link(NodeOutput _output, DialogueNode _to) {
            _output.Target = _to.Id;
        }

        /// <summary> Start → first line → second line </summary>
        private DialogueAsset TwoLines(out LineNode _first, out LineNode _second) {
            DialogueAsset _asset = NewAsset();
            _first = AddLine(_asset);
            _second = AddLine(_asset);
            Link(_asset.Start.Next, _first);
            Link(_first.Next, _second);
            return _asset;
        }

        private static void Tick() => Dialogue.TickAll(0f, 0f);

        /// <summary> Finishes the line on screen and moves the runs on </summary>
        private void AdvanceLine() {
            channel.Last.Advance();
            Tick();
        }
        #endregion

        // ==================== FOLLOWED RUN ====================
        #region Followed Run
        [Test]
        public void Tracker_FollowsMainByDefault() {
            DialogueRun _ambient = Dialogue.Start(TwoLines(out _, out _), new DialogueStartOptions { IsMain = false });
            DialogueRun _main = Dialogue.Start(TwoLines(out _, out _));

            tracker = new DialogueRunTracker();
            Assert.AreSame(_main, tracker.Followed, "a main run already running is followed");

            tracker.Dispose();
            Dialogue.StopAll();
            tracker = new DialogueRunTracker();
            Assert.IsNull(tracker.Followed);

            Dialogue.Start(TwoLines(out _, out _), new DialogueStartOptions { IsMain = false });
            Assert.IsNull(tracker.Followed, "ambient runs are not followed by default");
            DialogueRun _next = Dialogue.Start(TwoLines(out _, out _));
            Assert.AreSame(_next, tracker.Followed, "the main run that starts is followed");
            Assert.AreNotSame(_ambient, tracker.Followed);
        }

        [Test]
        public void Tracker_FollowedEnds_FollowsRunningMain() {
            DialogueRun _main = Dialogue.Start(TwoLines(out _, out _));
            DialogueRun _ambient = Dialogue.Start(TwoLines(out _, out _), new DialogueStartOptions { IsMain = false });
            tracker = new DialogueRunTracker { Followed = _ambient };

            _ambient.Stop();

            Assert.AreSame(_main, tracker.Followed);
        }
        #endregion

        // ==================== PATH ====================
        #region Path
        [Test]
        public void Tracker_RecordsTraversedOutputs_PerAsset() {
            DialogueAsset _asset = TwoLines(out LineNode _first, out LineNode _second);
            DialogueAsset _other = TwoLines(out _, out _);
            tracker = new DialogueRunTracker();
            int _changes = 0;
            tracker.Changed += () => _changes++;
            Dialogue.Start(_asset);

            Tick();
            Assert.AreEqual(_first.Id, tracker.CurrentNodeId);
            CollectionAssert.AreEquivalent(new[] { _asset.Start.Next.Id }, tracker.TraversedOutputIds(_asset));

            AdvanceLine();
            Assert.AreSame(_asset, tracker.CurrentAsset);
            Assert.AreEqual(_second.Id, tracker.CurrentNodeId);
            CollectionAssert.AreEquivalent(new[] { _asset.Start.Next.Id, _first.Next.Id }, tracker.TraversedOutputIds(_asset));
            CollectionAssert.IsEmpty(tracker.TraversedOutputIds(_other));
            Assert.Greater(_changes, 0);
        }

        [Test]
        public void Tracker_JumpAndReturn_SwitchesCurrentAsset() {
            DialogueAsset _target = NewAsset();
            LineNode _inside = AddLine(_target);
            Link(_target.Start.Next, _inside);
            DialogueAsset _asset = NewAsset();
            JumpNode _jump = _asset.AddNode<JumpNode>();
            _jump.Target = _target;
            _jump.Mode = JumpMode.CallAndReturn;
            LineNode _after = AddLine(_asset);
            Link(_asset.Start.Next, _jump);
            Link(_jump.Next, _after);
            tracker = new DialogueRunTracker();
            Dialogue.Start(_asset);

            Tick();
            Assert.AreSame(_target, tracker.CurrentAsset);
            Assert.AreEqual(_inside.Id, tracker.CurrentNodeId);
            CollectionAssert.AreEquivalent(new[] { _target.Start.Next.Id }, tracker.TraversedOutputIds(_target));
            CollectionAssert.AreEquivalent(new[] { _asset.Start.Next.Id }, tracker.TraversedOutputIds(_asset));

            AdvanceLine();
            Assert.AreSame(_asset, tracker.CurrentAsset);
            Assert.AreEqual(_after.Id, tracker.CurrentNodeId);
            CollectionAssert.AreEquivalent(new[] { _asset.Start.Next.Id, _jump.Next.Id }, tracker.TraversedOutputIds(_asset));
        }

        [Test]
        public void Tracker_RunEnds_KeepsPath() {
            DialogueAsset _asset = TwoLines(out LineNode _first, out _);
            tracker = new DialogueRunTracker();
            DialogueRun _run = Dialogue.Start(_asset);
            Tick();
            AdvanceLine();
            AdvanceLine();

            Assert.AreEqual(RunState.Ended, _run.State);
            Assert.AreSame(_run, tracker.Followed);
            Assert.AreSame(_asset, tracker.CurrentAsset);
            Assert.IsNull(tracker.CurrentNodeId, "an ended run has no current node");
            CollectionAssert.AreEquivalent(new[] { _asset.Start.Next.Id, _first.Next.Id }, tracker.TraversedOutputIds(_asset));

            DialogueRun _next = Dialogue.Start(_asset);
            Assert.AreSame(_next, tracker.Followed);
            CollectionAssert.IsEmpty(tracker.TraversedOutputIds(_asset), "the next followed run starts a new path");
        }
        #endregion
    }
}
