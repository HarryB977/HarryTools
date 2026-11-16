using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using HarryTools.Actions;
using HarryTools.Conditions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarryTools.TextEffects.Tests {
    public class TextEventQueueTests {
        // ==================== SETUP ===================
        [SetUp]
        public void SetUp() {
            RecordingWaitAction.Log.Clear();
        }

        private static ActionList List(string _label, int _frames, bool _throw = false) {
            return new ActionList(new RecordingWaitAction(_label, _frames, _throw));
        }

        private static IEnumerator WaitUntilIdle(TextEventQueue _queue, int _maxFrames = 60) {
            for (int _i = 0; _i < _maxFrames && _queue.IsRunning; _i++) yield return null;
        }

        // ==================== TESTS ===================
        [UnityTest]
        public IEnumerator Queue_RunsSequentially() {
            TextEventQueue _queue = new TextEventQueue();
            _queue.Enqueue(List("a", 3), LogicContext.Empty);
            _queue.Enqueue(List("b", 1), LogicContext.Empty);
            yield return WaitUntilIdle(_queue);

            CollectionAssert.AreEqual(new[] { "a:start", "a:end", "b:start", "b:end" }, RecordingWaitAction.Log);
            Assert.IsFalse(_queue.IsRunning);
            Assert.AreEqual(0, _queue.PendingCount);
        }

        [UnityTest]
        public IEnumerator Queue_EnqueueDuringRun_RunsAfter() {
            TextEventQueue _queue = new TextEventQueue();
            _queue.Enqueue(List("a", 5), LogicContext.Empty);
            yield return null;
            yield return null;
            Assert.IsTrue(_queue.IsRunning);
            _queue.Enqueue(List("b", 1), LogicContext.Empty);
            Assert.AreEqual(1, _queue.PendingCount);
            yield return WaitUntilIdle(_queue);

            CollectionAssert.AreEqual(new[] { "a:start", "a:end", "b:start", "b:end" }, RecordingWaitAction.Log);
        }

        [UnityTest]
        public IEnumerator Queue_Cancel_StopsCurrentAndClears() {
            TextEventQueue _queue = new TextEventQueue();
            _queue.Enqueue(List("a", 10), LogicContext.Empty);
            _queue.Enqueue(List("b", 1), LogicContext.Empty);
            yield return null;
            yield return null;

            _queue.Cancel();
            Assert.IsFalse(_queue.IsRunning);
            Assert.AreEqual(0, _queue.PendingCount);
            for (int _i = 0; _i < 15; _i++) yield return null;
            CollectionAssert.AreEqual(new[] { "a:start" }, RecordingWaitAction.Log);

            _queue.Enqueue(List("c", 1), LogicContext.Empty);
            yield return WaitUntilIdle(_queue);
            CollectionAssert.AreEqual(new[] { "a:start", "c:start", "c:end" }, RecordingWaitAction.Log);
        }

        [UnityTest]
        public IEnumerator Queue_ExceptionInOne_NextStillRuns() {
            LogAssert.Expect(LogType.Exception, new Regex(".*Test failure in bad.*", RegexOptions.Singleline));
            TextEventQueue _queue = new TextEventQueue();
            _queue.Enqueue(List("bad", 0, true), LogicContext.Empty);
            _queue.Enqueue(List("good", 1), LogicContext.Empty);
            yield return WaitUntilIdle(_queue);

            CollectionAssert.AreEqual(new[] { "bad:start", "good:start", "good:end" }, RecordingWaitAction.Log);
        }

        [Test]
        public void Table_TryGetEvent_IgnoresCase() {
            ActionList _list = List("x", 0);
            TextEventTable _table = new TextEventTable(new List<TextEventEntry> { new TextEventEntry("Door", _list, false) });

            Assert.IsTrue(_table.TryGetEvent("dOOR", out ActionList _found, out bool _runWhenSkipped));
            Assert.AreSame(_list, _found);
            Assert.IsFalse(_runWhenSkipped);
            Assert.IsTrue(_table.Contains("DOOR"));
            Assert.IsFalse(_table.TryGetEvent("other", out _, out _));
        }
    }
}
