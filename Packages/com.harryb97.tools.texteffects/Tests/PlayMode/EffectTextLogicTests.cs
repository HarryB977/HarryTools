using System;
using System.Collections;
using System.Collections.Generic;
using HarryTools.Actions;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarryTools.TextEffects.Tests {
    public class EffectTextLogicTests {
        // ==================== VARIABLES ===================
        private GameObject canvasObject;
        private TextEffectsSettings settings;
        private float savedTimeScale;
        private readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();

        // ==================== SETUP ===================
        [SetUp]
        public void SetUp() {
            savedTimeScale = Time.timeScale;
            RecordingWaitAction.Log.Clear();
            settings = ScriptableObject.CreateInstance<TextEffectsSettings>();
            settings.hideFlags = HideFlags.HideAndDontSave;
            TextEffectsSettings.UseForTests(settings);

            canvasObject = new GameObject("TextEffectsTestCanvas", typeof(Canvas));
        }

        [TearDown]
        public void TearDown() {
            Time.timeScale = savedTimeScale;
            if (canvasObject != null) UnityEngine.Object.DestroyImmediate(canvasObject);
            for (int _i = 0; _i < created.Count; _i++) {
                if (created[_i] != null) UnityEngine.Object.DestroyImmediate(created[_i]);
            }
            created.Clear();
            TextEffectsSettings.ClearForTests();
            UnityEngine.Object.DestroyImmediate(settings);
        }

        /// <summary> Builds the component while its object is inactive so fields can be set before OnEnable </summary>
        private EffectText Build(string _text, Action<EffectText> _configure = null) {
            GameObject _go = new GameObject("Text");
            _go.SetActive(false);
            _go.transform.SetParent(canvasObject.transform, false);
            _go.AddComponent<TextMeshProUGUI>();
            EffectText _effect = _go.AddComponent<EffectText>();
            _effect.PlainText = _text;
            _effect.CharactersPerSecond = 100f;
            _configure?.Invoke(_effect);
            _go.SetActive(true);
            return _effect;
        }

        private static ActionList List(string _label, int _frames = 0) {
            return new ActionList(new RecordingWaitAction(_label, _frames));
        }

        private static IEnumerator WaitFrames(int _frames) {
            for (int _i = 0; _i < _frames; _i++) yield return null;
        }

        // ==================== TESTS ===================
        [Test]
        public void ShowOnEnable_StartsTyping() {
            EffectText _effect = Build("hello");
            Assert.AreEqual(EffectTextState.Typing, _effect.State);
        }

        [Test]
        public void NoShowOnEnable_StaysHidden() {
            EffectText _effect = Build("hello", _e => _e.ShowOnEnable = false);
            Assert.AreEqual(EffectTextState.Hidden, _effect.State);
            Assert.AreEqual(0, _effect.VisibleCount);
            Assert.AreEqual("hello", _effect.Text);
        }

        [Test]
        public void SetText_ShowFalse_StaysHidden_ShowStarts() {
            EffectText _effect = Build("a", _e => _e.ShowOnEnable = false);
            _effect.SetText("hello", false);
            Assert.AreEqual(EffectTextState.Hidden, _effect.State);

            _effect.Show();
            Assert.AreEqual(EffectTextState.Typing, _effect.State);
            _effect.Tick(0.025f);
            Assert.Greater(_effect.VisibleCount, 0);
            Assert.Less(_effect.VisibleCount, 5);
        }

        [Test]
        public void Skip_ThenContinue_ReachesComplete_FiresTypingCompletedOnce() {
            EffectText _effect = Build("ab<waitinput>cd");
            int _waiting = 0;
            int _completed = 0;
            _effect.WaitingInput += () => _waiting++;
            _effect.TypingCompleted += () => _completed++;

            _effect.Skip();
            Assert.AreEqual(EffectTextState.WaitingInput, _effect.State);
            Assert.AreEqual(2, _effect.VisibleCount);
            Assert.AreEqual(1, _waiting);

            _effect.Skip();
            Assert.AreEqual(EffectTextState.WaitingInput, _effect.State);

            _effect.Continue();
            Assert.AreEqual(EffectTextState.Typing, _effect.State);
            _effect.Skip();
            Assert.AreEqual(EffectTextState.Complete, _effect.State);
            Assert.AreEqual(4, _effect.VisibleCount);

            _effect.Tick(5f);
            _effect.Continue();
            Assert.AreEqual(1, _completed);
            Assert.AreEqual(1, _waiting);
        }

        [UnityTest]
        public IEnumerator Event_EnqueuesActions_RunWhenSkippedFalse_SkippedNotEnqueued_ButEventReachedFires() {
            TextEventTable _table = new TextEventTable(new List<TextEventEntry> {
                new TextEventEntry("run", List("run"), false),
                new TextEventEntry("keep", List("keep"), true)
            });
            EffectText _effect = Build("a<event=run>b<event=keep>c", _e => _e.CharactersPerSecond = 1000f);
            _effect.SetEventProvider(_table);
            List<string> _reached = new List<string>();
            _effect.EventReached += _reached.Add;

            _effect.SetText("a<event=run>b<event=keep>c");
            _effect.Skip();
            yield return WaitFrames(3);

            CollectionAssert.AreEqual(new[] { "run", "keep" }, _reached);
            CollectionAssert.AreEqual(new[] { "keep:start", "keep:end" }, RecordingWaitAction.Log);

            RecordingWaitAction.Log.Clear();
            _reached.Clear();
            _effect.SetText("a<event=run>b<event=keep>c");
            _effect.Tick(1f);
            yield return WaitFrames(3);

            CollectionAssert.AreEqual(new[] { "run", "keep" }, _reached);
            CollectionAssert.AreEqual(new[] { "run:start", "run:end", "keep:start", "keep:end" }, RecordingWaitAction.Log);
        }

        [UnityTest]
        public IEnumerator SetEventProvider_UsesExternal() {
            EffectText _effect = Build("a<event=x>b", _e => {
                _e.Events = new TextEventTable(new List<TextEventEntry> { new TextEventEntry("x", List("own")) });
                _e.ShowOnEnable = false;
            });
            TextEventTable _external = new TextEventTable(new List<TextEventEntry> { new TextEventEntry("x", List("ext")) });

            _effect.SetEventProvider(_external);
            _effect.Show();
            _effect.Skip();
            yield return WaitFrames(3);
            CollectionAssert.AreEqual(new[] { "ext:start", "ext:end" }, RecordingWaitAction.Log);

            RecordingWaitAction.Log.Clear();
            _effect.SetEventProvider(null);
            _effect.Show();
            _effect.Skip();
            yield return WaitFrames(3);
            CollectionAssert.AreEqual(new[] { "own:start", "own:end" }, RecordingWaitAction.Log);
        }

        [UnityTest]
        public IEnumerator SetText_WhileQueueRunning_QueueKeepsOrder() {
            TextEventTable _table = new TextEventTable(new List<TextEventEntry> {
                new TextEventEntry("a", List("a", 5)),
                new TextEventEntry("b", List("b", 1))
            });
            EffectText _effect = Build("<event=a>x", _e => _e.ShowOnEnable = false);
            _effect.SetEventProvider(_table);

            _effect.Show();
            _effect.Skip();
            Assert.IsTrue(_effect.Queue.IsRunning);

            _effect.SetText("<event=b>y");
            Assert.IsTrue(_effect.Queue.IsRunning);
            _effect.Skip();
            Assert.AreEqual(1, _effect.Queue.PendingCount);

            for (int _i = 0; _i < 60 && _effect.Queue.IsRunning; _i++) yield return null;
            CollectionAssert.AreEqual(new[] { "a:start", "a:end", "b:start", "b:end" }, RecordingWaitAction.Log);
        }

        [UnityTest]
        public IEnumerator TimeMode_Unscaled_TypesWithTimeScaleZero() {
            EffectText _effect = Build("abc", _e => {
                _e.TimeMode = TimeMode.Unscaled;
                _e.CharactersPerSecond = 100000f;
            });
            Time.timeScale = 0f;
            for (int _i = 0; _i < 10 && _effect.State != EffectTextState.Complete; _i++) yield return null;

            Assert.AreEqual(EffectTextState.Complete, _effect.State);
            Assert.AreEqual(3, _effect.VisibleCount);
        }

        [UnityTest]
        public IEnumerator TimeMode_Scaled_DoesNotTypeWithTimeScaleZero() {
            EffectText _effect = Build("abc", _e => {
                _e.TimeMode = TimeMode.Scaled;
                _e.CharactersPerSecond = 100000f;
            });
            Time.timeScale = 0f;
            yield return WaitFrames(5);

            // The first character shows as soon as typing starts, then nothing advances without scaled time
            Assert.AreEqual(EffectTextState.Typing, _effect.State);
            Assert.LessOrEqual(_effect.VisibleCount, 1);
        }

        [Test]
        public void CharacterShown_CarriesActiveVoice() {
            TypingVoice _robot = ScriptableObject.CreateInstance<TypingVoice>();
            created.Add(_robot);
            _robot.TagName = "robot";
            settings.VoiceList.Add(_robot);
            settings.Invalidate();

            EffectText _effect = Build("<voice=robot>a", _e => _e.CharactersPerSecond = 10f);
            List<CharacterShownArgs> _shown = new List<CharacterShownArgs>();
            _effect.CharacterShown += _shown.Add;

            _effect.Tick(0.5f);

            Assert.AreEqual(1, _shown.Count);
            Assert.AreEqual('a', _shown[0].Character);
            Assert.AreEqual(0, _shown[0].Index);
            Assert.AreSame(_robot, _shown[0].Voice);
            Assert.IsFalse(_shown[0].Skipped);
        }

        [Test]
        public void HideImmediate_StateHidden_FiresHiddenOnce() {
            EffectText _effect = Build("hello");
            int _hidden = 0;
            _effect.Hidden += () => _hidden++;

            _effect.Skip();
            Assert.AreEqual(EffectTextState.Complete, _effect.State);

            _effect.HideImmediate();
            _effect.HideImmediate();
            _effect.Hide();

            Assert.AreEqual(EffectTextState.Hidden, _effect.State);
            Assert.AreEqual(0, _effect.VisibleCount);
            Assert.AreEqual(1, _hidden);
        }
    }
}
