using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Profiling;

namespace HarryTools.TextEffects.Tests {
    public class TypewriterTests {
        // ==================== VARIABLES ===================
        #region Private Variables
        private sealed class FakeLookup : ITagLookup {
            public bool IsPreset(string _name) { return false; }
            public bool IsVoice(string _name) { return string.Equals(_name, "robot", StringComparison.OrdinalIgnoreCase); }
        }

        private sealed class RecordingListener : ITypewriterListener {
            public readonly List<string> Calls = new List<string>();
            public int Completed;
            public int Waiting;

            public void OnCharacterShown(int _index, bool _skipped) { Calls.Add("char:" + _index + (_skipped ? ":s" : "")); }
            public void OnEvent(string _name, int _ordinal, bool _skipped) { Calls.Add("event:" + _name + ":" + _ordinal + (_skipped ? ":s" : "")); }
            public void OnVoiceChanged(string _voiceName) { Calls.Add("voice:" + _voiceName); }
            public void OnWaitingInput() { Waiting++; }
            public void OnCompleted() { Completed++; }
        }

        private sealed class CountingListener : ITypewriterListener {
            public int Count;
            public void OnCharacterShown(int _index, bool _skipped) { Count++; }
            public void OnEvent(string _name, int _ordinal, bool _skipped) { Count++; }
            public void OnVoiceChanged(string _voiceName) { Count++; }
            public void OnWaitingInput() { Count++; }
            public void OnCompleted() { Count++; }
        }

        private static readonly ITagLookup lookup = new FakeLookup();
        #endregion

        // ==================== HELPERS ====================
        #region Helpers
        private static Typewriter Make(string _markup, float _cps = 10f) {
            ParsedText parsed = TextParser.Parse(_markup, lookup);
            int count = parsed.TmpText.Length;
            int[] indices = new int[count];
            for (int i = 0; i < count; i++) indices[i] = i;

            Typewriter typewriter = new() { CharactersPerSecond = _cps };
            typewriter.Load(parsed, indices, count);
            typewriter.Start();
            return typewriter;
        }
        #endregion

        // ==================== TESTS ====================
        #region Timing
        [Test]
        public void Tick_FirstTickShowsFirstChar() {
            Typewriter tw = Make("abcd");
            tw.Tick(0f, null);
            Assert.AreEqual(1, tw.VisibleCount);
        }

        [Test]
        public void Tick_Accumulates() {
            Typewriter tw = Make("abcd");
            tw.Tick(0.25f, null);
            Assert.AreEqual(3, tw.VisibleCount);
        }

        [Test]
        public void Tick_LargeDt_ShowsSeveral_ThenComplete() {
            RecordingListener listener = new();
            Typewriter tw = Make("abcd");
            tw.Tick(10f, listener);
            Assert.AreEqual(4, tw.VisibleCount);
            Assert.AreEqual(TypewriterStatus.Complete, tw.Status);
            Assert.AreEqual(1, listener.Completed);
            tw.Tick(10f, listener);
            Assert.AreEqual(1, listener.Completed);
        }

        [Test]
        public void Pause_DelaysNext() {
            Typewriter tw = Make("ab<pause=1>c");
            tw.Tick(0.15f, null);
            Assert.AreEqual(2, tw.VisibleCount);
            tw.Tick(0.9f, null);
            Assert.AreEqual(2, tw.VisibleCount);
            tw.Tick(0.2f, null);
            Assert.AreEqual(3, tw.VisibleCount);
        }

        [Test]
        public void Speed_ChangesInterval() {
            Typewriter tw = Make("<speed=2>abcd");
            tw.Tick(0.15f, null);
            Assert.AreEqual(4, tw.VisibleCount);
        }

        [Test]
        public void Instant_ShowsBlockTogether() {
            Typewriter tw = Make("<instant>abc</>d");
            tw.Tick(0f, null);
            Assert.AreEqual(3, tw.VisibleCount);
        }
        #endregion

        #region Input and skip
        [Test]
        public void WaitInput_StopsUntilContinue() {
            RecordingListener listener = new();
            Typewriter tw = Make("ab<waitinput>cd");
            tw.Tick(10f, listener);
            Assert.AreEqual(2, tw.VisibleCount);
            Assert.AreEqual(TypewriterStatus.WaitingInput, tw.Status);
            Assert.AreEqual(1, listener.Waiting);
            tw.Continue();
            tw.Tick(10f, listener);
            Assert.AreEqual(4, tw.VisibleCount);
            Assert.AreEqual(TypewriterStatus.Complete, tw.Status);
        }

        [Test]
        public void Skip_StopsAtWaitInput_FiresEventsSkipped() {
            RecordingListener listener = new();
            Typewriter tw = Make("a<event=x>b<waitinput>c<event=y>");
            tw.Skip(listener);
            Assert.AreEqual(2, tw.VisibleCount);
            Assert.AreEqual(TypewriterStatus.WaitingInput, tw.Status);
            CollectionAssert.Contains(listener.Calls, "event:x:0:s");
            CollectionAssert.DoesNotContain(listener.Calls, "event:y:1:s");
        }

        [Test]
        public void Skip_IgnoresPauses_AppliesVoice() {
            RecordingListener listener = new();
            Typewriter tw = Make("a<pause=5><voice=robot>b");
            tw.Skip(listener);
            Assert.AreEqual(2, tw.VisibleCount);
            Assert.AreEqual(TypewriterStatus.Complete, tw.Status);
            CollectionAssert.Contains(listener.Calls, "voice:robot");
            Assert.AreEqual(1, listener.Completed);
        }

        [Test]
        public void Skip_WhileWaiting_DoesNothing() {
            Typewriter tw = Make("a<waitinput>b");
            tw.Tick(10f, null);
            Assert.AreEqual(TypewriterStatus.WaitingInput, tw.Status);
            tw.Skip(null);
            Assert.AreEqual(1, tw.VisibleCount);
            Assert.AreEqual(TypewriterStatus.WaitingInput, tw.Status);
        }

        [Test]
        public void ShowAllImmediate_NoEventsNoVoice_Completes() {
            RecordingListener listener = new();
            Typewriter tw = Make("a<event=x><voice=robot>b");
            tw.ShowAllImmediate(listener);
            Assert.AreEqual(2, tw.VisibleCount);
            Assert.AreEqual(TypewriterStatus.Complete, tw.Status);
            Assert.AreEqual(0, listener.Calls.Count);
            Assert.AreEqual(1, listener.Completed);
        }
        #endregion

        #region Events and edges
        [Test]
        public void Start_SuppressEventsBelow() {
            RecordingListener listener = new();
            Typewriter tw = Make("<event=a>x<event=b>y");
            tw.Start(1);
            tw.Tick(10f, listener);
            CollectionAssert.DoesNotContain(listener.Calls, "event:a:0");
            CollectionAssert.Contains(listener.Calls, "event:b:1");
            Assert.AreEqual(2, tw.NextEventOrdinal);
        }

        [Test]
        public void OnlyCommands_NoCharacters_FiresAndCompletes() {
            RecordingListener listener = new();
            Typewriter tw = Make("<event=a><pause=1><event=b>");
            tw.Tick(0f, listener);
            Assert.AreEqual(TypewriterStatus.Typing, tw.Status);
            CollectionAssert.AreEqual(new[] { "event:a:0" }, listener.Calls);
            tw.Tick(0.5f, listener);
            Assert.AreEqual(TypewriterStatus.Typing, tw.Status);
            tw.Tick(0.6f, listener);
            Assert.AreEqual(TypewriterStatus.Complete, tw.Status);
            CollectionAssert.AreEqual(new[] { "event:a:0", "event:b:1" }, listener.Calls);
            Assert.AreEqual(1, listener.Completed);
        }

        [Test]
        public void CommandAfterLastChar_RunsBeforeComplete() {
            RecordingListener listener = new();
            Typewriter tw = Make("ab<event=end>");
            tw.Tick(10f, listener);
            Assert.AreEqual("event:end:0", listener.Calls[2]);
            Assert.AreEqual(1, listener.Completed);
        }

        [Test]
        public void EmptyText_CompletesOnFirstTick() {
            RecordingListener listener = new();
            Typewriter tw = Make("");
            tw.Tick(0f, listener);
            Assert.AreEqual(TypewriterStatus.Complete, tw.Status);
            Assert.AreEqual(1, listener.Completed);
        }
        #endregion

        #region Allocations
        [Test]
        public void Tick_NoAllocations() {
            string text = new string('a', 1000) + "<pause=0.1><event=e><speed=2><voice=robot>" + new string('b', 1000);
            Typewriter tw = Make(text, 100f);
            CountingListener listener = new();
            tw.Tick(0f, listener);

            using ProfilerRecorder recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 1, ProfilerRecorderOptions.Default | ProfilerRecorderOptions.CollectOnlyOnCurrentThread);

            object[] sink = new object[1];
            long controlStart = recorder.CurrentValue;
            for (int i = 0; i < 1000; i++) sink = new object[1];
            long controlCount = recorder.CurrentValue - controlStart;
            Assert.Greater(controlCount, 0, "Positive control must register allocations, otherwise the measure is useless");
            Assert.IsNotNull(sink);

            long start = recorder.CurrentValue;
            for (int i = 0; i < 400; i++) tw.Tick(0.016f, listener);
            tw.Skip(listener);
            long allocated = recorder.CurrentValue - start;

            Assert.AreEqual(TypewriterStatus.Complete, tw.Status);
            Assert.AreEqual(0, allocated, "Tick must not allocate");
        }
        #endregion
    }
}
