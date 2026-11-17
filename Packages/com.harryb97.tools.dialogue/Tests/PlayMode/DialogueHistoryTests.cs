using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace HarryTools.Dialogue.Tests {
    public class DialogueHistoryTests {
        // ==================== SETUP ====================
        #region Setup
        private readonly List<DialogueAsset> assets = new List<DialogueAsset>();
        private readonly Dictionary<LineNode, string> texts = new Dictionary<LineNode, string>();
        private DialogueSettings settings;
        private FakeLineChannel channel;

        [SetUp]
        public void SetUp() {
            DialogueMemory.ClearForTests();
            settings = ScriptableObject.CreateInstance<DialogueSettings>();
            settings.HistoryEnabled = true;
            DialogueSettings.UseForTests(settings);
            Dialogue.ManualTick = true;
            channel = new FakeLineChannel();
            Dialogue.ChannelResolver = _ => channel;
            texts.Clear();
            Dialogue.LineTextResolver = _line => texts.TryGetValue(_line, out string _text) ? _text : null;
            DialogueHistory.Clear();
        }

        [TearDown]
        public void TearDown() {
            Dialogue.StopAll();
            Dialogue.ClearForTests();
            DialogueSettings.ClearForTests();
            DialogueMemory.ClearForTests();
            DialogueHistory.Clear();
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

        private LineNode AddLine(DialogueAsset _asset, string _text) {
            LineNode _line = _asset.AddNode<LineNode>();
            texts[_line] = _text;
            _line.OverrideAdvance = true;
            _line.Advance = AdvanceMode.Input;
            return _line;
        }

        private static void Link(NodeOutput _output, DialogueNode _to) {
            _output.Target = _to.Id;
        }

        private static void Tick() => Dialogue.TickAll(0f, 0f);

        /// <summary> A chain of lines that each wait for the player to advance </summary>
        private DialogueAsset Chain(int _count, out LineNode[] _lines) {
            DialogueAsset _asset = NewAsset();
            _lines = new LineNode[_count];
            NodeOutput _from = _asset.Start.Next;
            for (int _i = 0; _i < _count; _i++) {
                _lines[_i] = AddLine(_asset, "line" + _i);
                Link(_from, _lines[_i]);
                _from = _lines[_i].Next;
            }
            return _asset;
        }

        private static void RunToEnd(DialogueRun _run) {
            for (int _i = 0; _i < 50 && _run.State != RunState.Ended; _i++) {
                Tick();
                if (_run.State != RunState.Ended) _run.Advance();
            }
        }
        #endregion

        // ==================== TESTS ====================
        #region Tests
        [Test]
        public void Disabled_RecordsNothing() {
            settings.HistoryEnabled = false;
            RunToEnd(Dialogue.Start(Chain(2, out _)));
            Assert.AreEqual(0, DialogueHistory.Entries.Count);
        }

        [Test]
        public void Main_RecordsLinesAndChoices_AmbientIgnored() {
            settings.HistoryClear = HistoryClear.Never;
            DialogueAsset _asset = NewAsset();
            LineNode _first = AddLine(_asset, "first");
            ChoiceOption _option = new ChoiceOption();
            ChoiceNode _choice = _asset.AddNode<ChoiceNode>();
            _choice.Options.Add(_option);
            _asset.EnsureIds();
            LineNode _last = AddLine(_asset, "last");
            Link(_asset.Start.Next, _first);
            Link(_first.Next, _choice);
            Link(_option.Next, _last);

            Dialogue.Start(Chain(1, out _), new DialogueStartOptions { IsMain = false });
            DialogueRun _run = Dialogue.Start(_asset);
            Tick();
            _run.Advance();
            Tick();
            _run.CurrentChoice.Choose(0);
            Tick();

            List<HistoryEntry> _entries = new List<HistoryEntry>(DialogueHistory.Entries);
            Assert.AreEqual(3, _entries.Count);
            Assert.AreEqual(HistoryEntryKind.Line, _entries[0].Kind);
            Assert.AreEqual("first", _entries[0].GetText());
            Assert.AreSame(_asset, _entries[0].Dialogue);
            Assert.AreEqual(HistoryEntryKind.Choice, _entries[1].Kind);
            Assert.AreSame(_option.Text, _entries[1].Text);
            Assert.AreEqual(HistoryEntryKind.Line, _entries[2].Kind);
            Assert.AreEqual("last", _entries[2].GetText());
        }

        [Test]
        public void SkippedLines_NotRecorded() {
            settings.HistoryClear = HistoryClear.Never;
            DialogueRun _run = Dialogue.Start(Chain(3, out _));
            Tick();
            Assert.AreEqual(1, DialogueHistory.Entries.Count);
            _run.Skip();
            Tick();
            Tick();
            Assert.AreEqual(RunState.Ended, _run.State);
            Assert.AreEqual(1, DialogueHistory.Entries.Count);
            Assert.AreEqual("line0", DialogueHistory.Entries[0].GetText());
        }

        [Test]
        public void Max_TrimsOldest() {
            settings.HistoryMax = 2;
            settings.HistoryClear = HistoryClear.Never;
            RunToEnd(Dialogue.Start(Chain(4, out _)));
            Assert.AreEqual(2, DialogueHistory.Entries.Count);
            Assert.AreEqual("line2", DialogueHistory.Entries[0].GetText());
            Assert.AreEqual("line3", DialogueHistory.Entries[1].GetText());
        }

        [Test]
        public void OnConversationEnd_Clears_Never_Keeps() {
            settings.HistoryClear = HistoryClear.OnConversationEnd;
            RunToEnd(Dialogue.Start(Chain(2, out _)));
            Assert.AreEqual(0, DialogueHistory.Entries.Count);

            settings.HistoryClear = HistoryClear.Never;
            RunToEnd(Dialogue.Start(Chain(2, out _)));
            Assert.AreEqual(2, DialogueHistory.Entries.Count);
        }

        [Test]
        public void GetText_StripsEffectTags_KeepsTmpTags() {
            settings.HistoryClear = HistoryClear.Never;
            DialogueAsset _asset = Chain(1, out LineNode[] _lines);
            texts[_lines[0]] = "<pause=0.5>Hello <b>bold</b>";
            Dialogue.Start(_asset);
            Tick();
            Assert.AreEqual(1, DialogueHistory.Entries.Count);
            Assert.AreEqual("Hello <b>bold</b>", DialogueHistory.Entries[0].GetText());
        }

        [Test]
        public void GetText_Unresolvable_ReturnsEmpty() {
            settings.HistoryClear = HistoryClear.Never;
            Dialogue.LineTextResolver = null;
            Dialogue.Start(Chain(1, out _));
            Tick();
            Assert.AreEqual(string.Empty, DialogueHistory.Entries[0].GetText());
        }
        #endregion
    }
}
