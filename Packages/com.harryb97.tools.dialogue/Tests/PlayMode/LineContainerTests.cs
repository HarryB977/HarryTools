using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarryTools.Actions;
using HarryTools.TextEffects;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarryTools.Dialogue.Tests {
    public class LineContainerTests {
        // ==================== TEST VIEWS ====================
        #region Test Views
        /// <summary> Records the notifications it receives </summary>
        public sealed class RecordingLineView : LineView {
            public int Enters;
            public int Exits;
            public int LastIndex = -1;
            public int LastCount = -1;

            public override void OnEnter() => Enters++;
            public override void OnExit() => Exits++;

            public override void OnIndexChanged(int _index, int _count) {
                LastIndex = _index;
                LastCount = _count;
            }
        }

        /// <summary> Counts its refreshes </summary>
        public sealed class RecordingChoiceView : ChoiceView {
            public int Refreshes;
            protected override void Refresh() => Refreshes++;
        }
        #endregion

        // ==================== SETUP ====================
        #region Setup
        private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        private readonly List<Object> created = new List<Object>();
        private readonly Dictionary<LineNode, string> texts = new Dictionary<LineNode, string>();
        private readonly List<EndReason> endReasons = new List<EndReason>();
        private readonly List<LineNode> ended = new List<LineNode>();
        private DialogueSettings settings;
        private TextEffectsSettings textSettings;
        private GameObject canvas;
        private RecordingLineView prefab;

        [SetUp]
        public void SetUp() {
            RecordingAction.Log.Clear();
            RecordingAction.LastSelf = null;
            DialogueMemory.ClearForTests();
            settings = ScriptableObject.CreateInstance<DialogueSettings>();
            DialogueSettings.UseForTests(settings);
            textSettings = ScriptableObject.CreateInstance<TextEffectsSettings>();
            textSettings.hideFlags = HideFlags.HideAndDontSave;
            typeof(TextEffectsSettings).GetMethod("UseForTests", Hidden).Invoke(null, new object[] { textSettings });

            Dialogue.ManualTick = true;
            texts.Clear();
            Dialogue.LineTextResolver = _line => texts.TryGetValue(_line, out string _text) ? _text : null;
            endReasons.Clear();
            ended.Clear();
            Dialogue.Ended += OnEnded;
            Dialogue.LineEnded += OnLineEnded;

            canvas = Track(new GameObject("LineContainerTestsCanvas", typeof(Canvas)));
            prefab = BuildPrefab();
        }

        [TearDown]
        public void TearDown() {
            Dialogue.Ended -= OnEnded;
            Dialogue.LineEnded -= OnLineEnded;
            Dialogue.StopAll();
            Dialogue.ClearForTests();
            DialogueSettings.ClearForTests();
            DialogueMemory.ClearForTests();
            for (int _i = created.Count - 1; _i >= 0; _i--) {
                if (created[_i] != null) Object.DestroyImmediate(created[_i]);
            }
            created.Clear();
            typeof(TextEffectsSettings).GetMethod("ClearForTests", Hidden).Invoke(null, null);
            Object.DestroyImmediate(textSettings);
            Object.DestroyImmediate(settings);
        }

        private void OnEnded(DialogueRun _run, EndReason _reason) => endReasons.Add(_reason);
        private void OnLineEnded(DialogueRun _run, LineNode _line) => ended.Add(_line);

        private T Track<T>(T _object) where T : Object {
            created.Add(_object);
            return _object;
        }
        #endregion

        // ==================== HELPERS ====================
        #region Helpers
        private static void SetField(object _target, string _name, object _value) {
            _target.GetType().GetField(_name, Hidden).SetValue(_target, _value);
        }

        /// <summary> Inactive template: TextMeshProUGUI + EffectText (no show on enable, 20 characters per second) + view </summary>
        private RecordingLineView BuildPrefab() {
            GameObject _object = Track(new GameObject("LineViewTemplate", typeof(RectTransform)));
            _object.SetActive(false);
            _object.transform.SetParent(canvas.transform, false);
            _object.AddComponent<TextMeshProUGUI>();
            EffectText _text = _object.AddComponent<EffectText>();
            SetField(_text, "showOnEnable", false);
            SetField(_text, "charactersPerSecond", 20f);
            RecordingLineView _view = _object.AddComponent<RecordingLineView>();
            _view.Text = _text;
            return _view;
        }

        private LineContainer BuildContainer(int _maxVisible, AdvanceMode _advance, LineLifetime? _lifetime = null, bool _isMain = true) {
            GameObject _object = Track(new GameObject("Container", typeof(RectTransform)));
            _object.SetActive(false);
            _object.transform.SetParent(canvas.transform, false);
            LineContainer _container = _object.AddComponent<LineContainer>();
            _container.IsMain = _isMain;
            _container.MaxVisible = _maxVisible;
            _container.Advance = _advance;
            if (_lifetime.HasValue) _container.Lifetime = _lifetime.Value;
            _container.LinePrefab = prefab;
            _container.LineParent = (RectTransform)_object.transform;
            _object.SetActive(true);
            return _container;
        }

        private static LineLifetime Fixed(float _seconds) {
            LineLifetime _lifetime = LineLifetime.Default;
            _lifetime.Mode = LineLifetimeMode.Fixed;
            _lifetime.Seconds = _seconds;
            return _lifetime;
        }

        private DialogueAsset NewAsset() {
            return Track(ScriptableObject.CreateInstance<DialogueAsset>());
        }

        /// <summary> Start → one line per text, in order </summary>
        private DialogueAsset Lines(out LineNode[] _lines, params string[] _texts) {
            DialogueAsset _asset = NewAsset();
            _lines = new LineNode[_texts.Length];
            NodeOutput _previous = _asset.Start.Next;
            for (int _i = 0; _i < _texts.Length; _i++) {
                LineNode _line = _asset.AddNode<LineNode>();
                _line.WriterNote = "line" + _i;
                texts[_line] = _texts[_i];
                _previous.Target = _line.Id;
                _previous = _line.Next;
                _lines[_i] = _line;
            }
            return _asset;
        }

        private static DialogueRun StartMain(DialogueAsset _asset, DialogueStartOptions _options = null) {
            _options = _options ?? new DialogueStartOptions();
            _options.IsMain = true;
            DialogueRun _run = Dialogue.Start(_asset, _options);
            Assert.IsNotNull(_run);
            Tick();
            return _run;
        }

        private static void Tick(float _dt = 0f) => Dialogue.TickAll(_dt, _dt);

        private static IEnumerator Frames(int _count) {
            for (int _i = 0; _i < _count; _i++) yield return null;
        }

        private static EffectText TextOf(LineContainer _container, int _index) => _container.Visible[_index].Text;

        private static TextEventTable Events(params TextEventEntry[] _entries) {
            return new TextEventTable(new List<TextEventEntry>(_entries));
        }

        /// <summary> An event that records "{name}" and "{name}:done" </summary>
        private static TextEventEntry Event(string _name, bool _runWhenSkipped = true) {
            return new TextEventEntry(_name, new ActionList(new RecordingAction(_name)), _runWhenSkipped);
        }
        #endregion

        // ==================== TESTS ====================
        #region Tests
        [UnityTest]
        public IEnumerator Max1_ReplacesPrevious() {
            LineContainer _container = BuildContainer(1, AdvanceMode.Input);
            List<LineView> _removed = new List<LineView>();
            _container.LineRemoved += _removed.Add;
            DialogueAsset _asset = Lines(out LineNode[] _lines, "first", "second", "third");
            yield return null;

            DialogueRun _run = StartMain(_asset);
            Assert.AreEqual(1, _container.Visible.Count);
            LineView _first = _container.Visible[0];
            Assert.AreSame(_lines[0], _first.Current.Line);

            _run.Advance();
            _run.Advance();
            Tick();
            Assert.AreEqual(1, _container.Visible.Count);
            Assert.AreSame(_lines[1], _container.Visible[0].Current.Line);
            CollectionAssert.AreEqual(new[] { _first }, _removed);
            Assert.AreEqual(1, ((RecordingLineView)_first).Exits);

            _run.Advance();
            _run.Advance();
            Tick();
            Assert.AreSame(_lines[2], _container.Visible[0].Current.Line);
            // The pool reuses the views: no view was created per line after the first ones
            Assert.LessOrEqual(_container.LineParent.childCount, 2);
            Assert.AreEqual(1, CountActive(_container.LineParent));
        }

        [UnityTest]
        public IEnumerator MaxN_OldestLeaves_WhenFull() {
            LineContainer _container = BuildContainer(2, AdvanceMode.Input);
            int _moves = 0;
            List<LineNode> _removed = new List<LineNode>();
            _container.LinesMoved += () => _moves++;
            _container.LineRemoved += _view => _removed.Add(_view.Current.Line);
            DialogueAsset _asset = Lines(out LineNode[] _lines, "a", "b", "c");
            yield return null;

            DialogueRun _run = StartMain(_asset);
            _run.Advance();
            _run.Advance();
            Tick();
            Assert.AreEqual(2, _container.Visible.Count);
            Assert.AreSame(_lines[0], _container.Visible[0].Current.Line);
            Assert.AreSame(_lines[1], _container.Visible[1].Current.Line);
            CollectionAssert.IsEmpty(_removed);
            int _movesBefore = _moves;

            _run.Advance();
            _run.Advance();
            Tick();
            Assert.AreEqual(2, _container.Visible.Count);
            Assert.AreSame(_lines[1], _container.Visible[0].Current.Line);
            Assert.AreSame(_lines[2], _container.Visible[1].Current.Line);
            CollectionAssert.AreEqual(new[] { _lines[0] }, _removed);

            // Visible order matches the sibling order and every view knows its index
            RecordingLineView _older = (RecordingLineView)_container.Visible[0];
            RecordingLineView _newest = (RecordingLineView)_container.Visible[1];
            Assert.Less(_older.transform.GetSiblingIndex(), _newest.transform.GetSiblingIndex());
            Assert.AreEqual(0, _older.LastIndex);
            Assert.AreEqual(1, _newest.LastIndex);
            Assert.AreEqual(2, _newest.LastCount);
            Assert.Greater(_moves, _movesBefore);
            // Three lines, two views: the view of the line that left was reused
            Assert.AreEqual(2, _container.LineParent.childCount);
        }

        [UnityTest]
        public IEnumerator Lifetime_StartsAfterTypingCompleted() {
            LineContainer _container = BuildContainer(1, AdvanceMode.Auto, Fixed(1f));
            DialogueAsset _asset = Lines(out LineNode[] _lines, "abcdefghijklmnopqrstuvwxyz");
            yield return null;

            DialogueRun _run = StartMain(_asset);
            EffectText _text = TextOf(_container, 0);
            Assert.AreEqual(EffectTextState.Typing, _text.State);

            // Time passing while typing does not count
            Tick(5f);
            Assert.AreSame(_lines[0], _run.Current);
            CollectionAssert.IsEmpty(ended);

            _text.Skip();
            Assert.AreEqual(EffectTextState.Complete, _text.State);
            Tick(0.5f);
            CollectionAssert.IsEmpty(ended);
            Tick(0.6f);
            CollectionAssert.AreEqual(new[] { _lines[0] }, ended);
        }

        [UnityTest]
        public IEnumerator Advance_Input_FirstCompletes_SecondFinishes() {
            LineContainer _container = BuildContainer(1, AdvanceMode.Input, Fixed(0.1f));
            DialogueAsset _asset = Lines(out LineNode[] _lines, "abcdefghijklmnopqrstuvwxyz");
            yield return null;

            DialogueRun _run = StartMain(_asset);
            EffectText _text = TextOf(_container, 0);

            _run.Advance();
            Assert.AreEqual(EffectTextState.Complete, _text.State);
            Tick(10f);
            CollectionAssert.IsEmpty(ended, "An input line does not end with its lifetime");

            _run.Advance();
            Tick();
            CollectionAssert.AreEqual(new[] { _lines[0] }, ended);
            CollectionAssert.AreEqual(new[] { EndReason.Completed }, endReasons);
        }

        [UnityTest]
        public IEnumerator Advance_WaitInput_Continues() {
            LineContainer _container = BuildContainer(1, AdvanceMode.Input);
            DialogueAsset _asset = Lines(out LineNode[] _lines, "ab<waitinput>cd");
            yield return null;

            DialogueRun _run = StartMain(_asset);
            EffectText _text = TextOf(_container, 0);

            _run.Advance();
            Assert.AreEqual(EffectTextState.WaitingInput, _text.State);
            _run.Advance();
            Assert.AreEqual(EffectTextState.Typing, _text.State);
            _run.Advance();
            Assert.AreEqual(EffectTextState.Complete, _text.State);
            Tick();
            CollectionAssert.IsEmpty(ended);

            _run.Advance();
            Tick();
            CollectionAssert.AreEqual(new[] { _lines[0] }, ended);
        }

        [UnityTest]
        public IEnumerator Advance_Auto_FinishesAfterLifetime() {
            LineContainer _container = BuildContainer(1, AdvanceMode.Auto, Fixed(1f));
            DialogueAsset _asset = Lines(out LineNode[] _lines, "abcdefghijklmnopqrstuvwxyz");
            yield return null;

            DialogueRun _run = StartMain(_asset);
            EffectText _text = TextOf(_container, 0);

            _run.Advance();
            Assert.AreEqual(EffectTextState.Typing, _text.State, "Input does not touch an automatic line");

            _text.Skip();
            _run.Advance();
            Tick(0.9f);
            CollectionAssert.IsEmpty(ended);
            Tick(0.2f);
            CollectionAssert.AreEqual(new[] { _lines[0] }, ended);
        }

        [UnityTest]
        public IEnumerator LineOverride_Wins() {
            LineContainer _container = BuildContainer(1, AdvanceMode.Input, Fixed(100f));
            DialogueAsset _asset = Lines(out LineNode[] _lines, "abc", "def");
            _lines[0].OverrideAdvance = true;
            _lines[0].Advance = AdvanceMode.Auto;
            _lines[0].OverrideLifetime = true;
            _lines[0].Lifetime = Fixed(0.5f);
            yield return null;

            DialogueRun _run = StartMain(_asset);
            TextOf(_container, 0).Skip();
            Tick(0.6f);
            CollectionAssert.AreEqual(new[] { _lines[0] }, ended);

            // The second line has no override: the container's input advance and long lifetime apply
            Tick();
            Assert.AreSame(_lines[1], _container.Visible[0].Current.Line);
            TextOf(_container, 0).Skip();
            Tick(50f);
            Assert.AreEqual(1, ended.Count);
            _run.Advance();
            Tick();
            Assert.AreEqual(2, ended.Count);
        }

        [UnityTest]
        public IEnumerator Bind_SetsVoiceEventsAndContext() {
            LineContainer _container = BuildContainer(1, AdvanceMode.Input);
            DialogueCharacter _character = Track(ScriptableObject.CreateInstance<DialogueCharacter>());
            TypingVoice _voice = Track(ScriptableObject.CreateInstance<TypingVoice>());
            SetField(_character, "typingVoice", _voice);
            GameObject _actorObject = Track(new GameObject("Actor"));
            _actorObject.AddComponent<DialogueActor>().Character = _character;

            DialogueAsset _asset = Lines(out LineNode[] _lines, "a<event=x>b");
            _lines[0].Character = _character;
            _lines[0].Events = new TextEventTable(new List<TextEventEntry> {
                new TextEventEntry("x", new ActionList(new RecordingAction("x")), true)
            });
            yield return null;

            DialogueRun _run = StartMain(_asset, new DialogueStartOptions { Time = DialogueTime.Scaled });
            EffectText _text = TextOf(_container, 0);
            Assert.AreSame(_voice, _text.DefaultVoice);
            Assert.AreEqual(TimeMode.Scaled, _text.TimeMode);

            _run.Advance();
            yield return Frames(3);
            CollectionAssert.AreEqual(new[] { "x", "x:done" }, RecordingAction.Log);
            Assert.AreSame(_actorObject, RecordingAction.LastSelf);
        }

        [UnityTest]
        public IEnumerator TextEvents_ActorAsSelf_OriginAsTarget() {
            LineContainer _container = BuildContainer(1, AdvanceMode.Input);
            DialogueCharacter _character = Track(ScriptableObject.CreateInstance<DialogueCharacter>());
            GameObject _actorObject = Track(new GameObject("Actor"));
            _actorObject.AddComponent<DialogueActor>().Character = _character;
            GameObject _origin = Track(new GameObject("Origin"));

            DialogueAsset _asset = Lines(out LineNode[] _lines, "a<event=x>b", "c<event=y>d");
            _lines[0].Character = _character;
            _lines[0].Events = Events(Event("x"));
            _lines[1].Events = Events(Event("y"));
            yield return null;

            DialogueRun _run = StartMain(_asset, new DialogueStartOptions { Origin = _origin });
            _run.Advance();
            yield return Frames(3);
            CollectionAssert.AreEqual(new[] { "x", "x:done" }, RecordingAction.Log);
            Assert.AreSame(_actorObject, RecordingAction.LastSelf);
            Assert.AreSame(_origin, RecordingAction.LastTarget);

            _run.Advance();
            Tick();
            Assert.AreSame(_lines[1], _container.Visible[0].Current.Line);
            RecordingAction.LastTarget = null;
            _run.Advance();
            yield return Frames(3);
            CollectionAssert.AreEqual(new[] { "x", "x:done", "y", "y:done" }, RecordingAction.Log);
            Assert.IsNull(RecordingAction.LastSelf, "Without an actor Self is empty");
            Assert.AreSame(_origin, RecordingAction.LastTarget);
        }

        [UnityTest]
        public IEnumerator DetachedLine_Skip_RunsTextEvents() {
            LineContainer _container = BuildContainer(1, AdvanceMode.Input);
            DialogueAsset _asset = Lines(out LineNode[] _lines, "a<event=x>b<event=n>c<event=y>d");
            _lines[0].Events = Events(Event("x"), Event("n", false), Event("y"));
            _lines[0].OnEnd.Actions = new ActionList(new RecordingAction("end"));
            // Nothing is typed, so no event fires before the container goes
            LineView.HoldTextForTests = true;
            yield return null;

            DialogueRun _run = StartMain(_asset);
            Assert.AreEqual(1, _container.Visible.Count);
            Object.Destroy(_container.gameObject);
            yield return null;

            _run.Skip();
            yield return Frames(3);
            CollectionAssert.AreEqual(new[] { "x", "x:done", "y", "y:done", "end", "end:done" }, RecordingAction.Log);
            CollectionAssert.AreEqual(new[] { EndReason.Skipped }, endReasons);
        }

        [UnityTest]
        public IEnumerator DetachedLine_Finish_RunsTextEventsNotFiredYet() {
            LineContainer _container = BuildContainer(1, AdvanceMode.Input);
            DialogueAsset _asset = Lines(out LineNode[] _lines, "a<event=x>b<waitinput>c<event=y>d");
            _lines[0].Events = Events(Event("x"), Event("y", false));
            yield return null;

            DialogueRun _run = StartMain(_asset);
            _run.Advance();
            Assert.AreEqual(EffectTextState.WaitingInput, TextOf(_container, 0).State);
            yield return Frames(2);
            CollectionAssert.AreEqual(new[] { "x", "x:done" }, RecordingAction.Log);

            Object.Destroy(_container.gameObject);
            yield return null;

            // Played to its end without view: the events after the ones the view fired
            _run.Advance();
            Tick();
            yield return Frames(2);
            CollectionAssert.AreEqual(new[] { "x", "x:done", "y", "y:done" }, RecordingAction.Log);
            CollectionAssert.AreEqual(new[] { EndReason.Completed }, endReasons);
        }

        [UnityTest]
        public IEnumerator Skip_ClearsRunLines_FromAnyPhase() {
            LineContainer _container = BuildContainer(3, AdvanceMode.Input);
            LineContainer _ambientContainer = BuildContainer(3, AdvanceMode.Input, null, false);
            _ambientContainer.IsDefaultAmbient = true;
            yield return null;

            string[] _phases = { "typing", "delay after", "end actions" };
            for (int _p = 0; _p < _phases.Length; _p++) {
                string _phase = _phases[_p];
                DialogueAsset _asset = Lines(out LineNode[] _lines, "first line", "second line");
                ChoiceNode _choice = _asset.AddNode<ChoiceNode>();
                _choice.Options.Add(new ChoiceOption());
                _asset.EnsureIds();
                _lines[1].Next.Target = _choice.Id;
                _lines[1].DelayAfter = 10f;
                _lines[1].OnEnd.Actions = new ActionList(new RecordingAction("end", _phase == "end actions" ? 3 : 0));

                DialogueRun _run = StartMain(_asset);
                DialogueRun _ambient = Dialogue.Start(Lines(out _, "ambient line"), new DialogueStartOptions { IsMain = false });
                Tick();
                Assert.AreEqual(1, _ambientContainer.Visible.Count, _phase);

                // The first line ends and stays on screen; the second one types
                _run.Advance();
                _run.Advance();
                Tick();
                if (_phase != "typing") {
                    _run.Advance();
                    _run.Advance();
                    Tick();
                }
                Assert.AreEqual(2, _container.Visible.Count, _phase);

                int _visibleAtChoice = -1;
                _run.ChoicePresented += (_, _) => _visibleAtChoice = _container.Visible.Count;
                _run.Skip();
                Assert.AreEqual(RunState.WaitingChoice, _run.State, _phase);
                Assert.AreEqual(0, _visibleAtChoice, _phase);
                Assert.AreEqual(0, _container.Visible.Count, _phase);
                Assert.AreEqual(1, _ambientContainer.Visible.Count, _phase + ": the lines of other runs stay");

                _run.Stop();
                _ambient.Stop();
                yield return Frames(5);
            }
        }

        [UnityTest]
        public IEnumerator Skip_RevealsWholeText_EventsOnce_LineLeaves() {
            LineContainer _container = BuildContainer(1, AdvanceMode.Input);
            DialogueAsset _asset = Lines(out LineNode[] _lines, "a<event=x>b<waitinput>c<event=y>d");
            _lines[0].Events = new TextEventTable(new List<TextEventEntry> {
                new TextEventEntry("x", new ActionList(new RecordingAction("x", 3)), true),
                new TextEventEntry("y", new ActionList(new RecordingAction("y")), true)
            });
            yield return null;

            DialogueRun _run = StartMain(_asset);
            LineView _view = _container.Visible[0];
            _run.Skip();
            Assert.AreEqual(0, _container.Visible.Count);
            Assert.IsFalse(_view.gameObject.activeSelf, "The view is back in the pool");
            CollectionAssert.AreEqual(new[] { EndReason.Skipped }, endReasons);

            // The events keep running in order after the view went back to the pool
            yield return Frames(8);
            CollectionAssert.AreEqual(new[] { "x", "x:done", "y", "y:done" }, RecordingAction.Log);
        }

        [UnityTest]
        public IEnumerator Skip_WhilePreparing_StillFiresTextEvents() {
            LineContainer _container = BuildContainer(1, AdvanceMode.Input);
            DialogueAsset _asset = Lines(out LineNode[] _lines, "a<event=x>b<event=n>c");
            _lines[0].Events = new TextEventTable(new List<TextEventEntry> {
                new TextEventEntry("x", new ActionList(new RecordingAction("x", 2)), true),
                new TextEventEntry("n", new ActionList(new RecordingAction("n")), false)
            });
            _lines[0].OnEnd.Actions = new ActionList(new RecordingAction("end"));
            LineView.HoldTextForTests = true;
            yield return null;

            DialogueRun _run = StartMain(_asset);
            Assert.IsTrue(_container.Visible[0].IsPreparing);
            _run.Skip();
            Assert.AreEqual(0, _container.Visible.Count);

            yield return Frames(8);
            CollectionAssert.AreEqual(new[] { "x", "x:done", "end", "end:done" }, RecordingAction.Log);
        }

        [UnityTest]
        public IEnumerator Skip_PresentedLineEvents_RunBeforeOnEndInOrder() {
            LineContainer _container = BuildContainer(1, AdvanceMode.Input);
            DialogueAsset _asset = Lines(out LineNode[] _lines, "a<event=x>b<waitinput>c<event=n>d<event=y>e", "next");
            _lines[0].Events = new TextEventTable(new List<TextEventEntry> {
                new TextEventEntry("x", new ActionList(new RecordingAction("x", 3)), true),
                new TextEventEntry("y", new ActionList(new RecordingAction("y", 2)), true),
                new TextEventEntry("n", new ActionList(new RecordingAction("n")), false)
            });
            _lines[0].OnEnd.Actions = new ActionList(new RecordingAction("end"));
            _lines[1].Before.Actions = new ActionList(new RecordingAction("next"));
            yield return null;

            DialogueRun _run = StartMain(_asset);
            Assert.AreEqual(1, _container.Visible.Count);
            _run.Skip();

            yield return Frames(15);
            CollectionAssert.AreEqual(new[] { "x", "x:done", "y", "y:done", "end", "end:done", "next", "next:done" }, RecordingAction.Log);
            CollectionAssert.AreEqual(new[] { EndReason.Skipped }, endReasons);
        }

        [UnityTest]
        public IEnumerator Pause_PausesEffectText() {
            LineContainer _container = BuildContainer(1, AdvanceMode.Input);
            DialogueAsset _asset = Lines(out _, "abcdefghijklmnopqrstuvwxyz");
            yield return null;

            DialogueRun _run = StartMain(_asset);
            EffectText _text = TextOf(_container, 0);
            Assert.IsFalse(_text.IsPaused);

            _run.Pause();
            Assert.IsTrue(_text.IsPaused);
            _run.Resume();
            Assert.IsFalse(_text.IsPaused);
        }

        [UnityTest]
        public IEnumerator ContainerDestroyedMidLine_RunContinues() {
            LineContainer _container = BuildContainer(1, AdvanceMode.Input);
            DialogueAsset _asset = Lines(out LineNode[] _lines, "abcdefghijklmnopqrstuvwxyz", "second");
            yield return null;

            DialogueRun _run = StartMain(_asset);
            Assert.AreEqual(1, _container.Visible.Count);

            Object.Destroy(_container.gameObject);
            yield return null;

            Tick(1f);
            _run.Pause();
            _run.Resume();
            // The next line has no container: it goes on without view
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("no main line container"));
            _run.Advance();
            Tick();
            CollectionAssert.AreEqual(new[] { _lines[0] }, ended);
            _run.Advance();
            Tick();
            CollectionAssert.AreEqual(new[] { EndReason.Completed }, endReasons);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ChoiceView_ShowRefreshHide() {
            LineContainer _container = BuildContainer(1, AdvanceMode.Input);
            GameObject _viewObject = Track(new GameObject("ChoiceView", typeof(RectTransform)));
            _viewObject.SetActive(false);
            _viewObject.transform.SetParent(canvas.transform, false);
            RecordingChoiceView _view = _viewObject.AddComponent<RecordingChoiceView>();
            _container.ChoiceView = _view;

            DialogueAsset _asset = NewAsset();
            ChoiceNode _choice = _asset.AddNode<ChoiceNode>();
            ChoiceOption _open = new ChoiceOption();
            ChoiceOption _locked = new ChoiceOption { Available = new HarryTools.Conditions.ConditionList(HarryTools.Conditions.ConditionList.EvaluationMode.All, new FixedCondition(false)) };
            ChoiceOption _other = new ChoiceOption();
            _choice.Options.AddRange(new[] { _open, _locked, _other });
            _asset.EnsureIds();
            _asset.Start.Next.Target = _choice.Id;
            yield return null;

            DialogueRun _run = StartMain(_asset);
            Assert.AreSame(_run.CurrentChoice, _view.Prompt);
            Assert.IsTrue(_viewObject.activeSelf);
            Assert.AreEqual(1, _view.Refreshes);
            Assert.AreEqual(string.Empty, _view.GetLockedReason(0));
            Assert.AreEqual(string.Empty, _view.GetOptionText(1));

            ChoicePrompt _prompt = _view.Prompt;
            _prompt.MoveSelection(1);
            Assert.AreEqual(2, _view.Refreshes);

            _prompt.Choose(2);
            Assert.IsNull(_view.Prompt);
            Assert.IsFalse(_viewObject.activeSelf);
            Assert.AreEqual(2, _view.Refreshes);
            Tick();
            CollectionAssert.AreEqual(new[] { EndReason.Completed }, endReasons);
        }
        #endregion

        private static int CountActive(Transform _parent) {
            int _count = 0;
            for (int _i = 0; _i < _parent.childCount; _i++) {
                if (_parent.GetChild(_i).gameObject.activeSelf) _count++;
            }
            return _count;
        }
    }
}
