using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using HarryTools.Actions;
using HarryTools.Conditions;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarryTools.TextEffects.Tests {
    public class EffectTextBeta2Tests {
        // ==================== VARIABLES ===================
        private GameObject canvasObject;
        private TextEffectsSettings settings;
        private float savedTimeScale;
        private readonly List<UnityEngine.Object> created = new List<UnityEngine.Object>();

        /// <summary> Test action that stores the 'Self' it receives </summary>
        [Serializable]
        private sealed class SelfRecordingAction : IAction {
            public static GameObject LastSelf;
            public static int Runs;

            public UniTask Run(LogicContext _context, CancellationToken _token) {
                LastSelf = _context.Self;
                Runs++;
                return UniTask.CompletedTask;
            }
        }

        // ==================== SETUP ===================
        [SetUp]
        public void SetUp() {
            savedTimeScale = Time.timeScale;
            SelfRecordingAction.LastSelf = null;
            SelfRecordingAction.Runs = 0;
            settings = ScriptableObject.CreateInstance<TextEffectsSettings>();
            settings.hideFlags = HideFlags.HideAndDontSave;
            TextEffectsSettings.UseForTests(settings);
            canvasObject = new GameObject("TextEffectsBeta2Canvas", typeof(Canvas));
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

        private TextEffectPreset FadeOutPreset() {
            FadeTransition _fade = new FadeTransition();
            string[][] _pairs = { new[] { "direction", "Out" }, new[] { "duration", "0.5" }, new[] { "ease", "Linear" } };
            for (int _i = 0; _i < _pairs.Length; _i++) {
                Assert.IsTrue(EffectParamBinder.TryApply(_fade, _pairs[_i][0], _pairs[_i][1], out string _error), _error);
            }
            TextEffectPreset _preset = ScriptableObject.CreateInstance<TextEffectPreset>();
            _preset.hideFlags = HideFlags.HideAndDontSave;
            created.Add(_preset);
            typeof(TextEffectPreset).GetField("tagName", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_preset, "fadeout");
            typeof(TextEffectPreset).GetField("effect", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_preset, _fade);
            return _preset;
        }

        private static IEnumerator WaitFrames(int _frames) {
            for (int _i = 0; _i < _frames; _i++) yield return null;
        }

        private EffectText BuildWithEvent() {
            TextEventTable _table = new TextEventTable(new List<TextEventEntry> {
                new TextEventEntry("go", new ActionList(new SelfRecordingAction()), true)
            });
            EffectText _effect = Build("a<event=go>b", _e => _e.CharactersPerSecond = 1000f);
            _effect.SetEventProvider(_table);
            _effect.SetText("a<event=go>b");
            return _effect;
        }

        // ==================== TESTS ===================
        [UnityTest]
        public IEnumerator TimeMode_SetAtRuntime_UsedByUpdate() {
            Time.timeScale = 0f;
            yield return WaitFrames(2);
            EffectText _effect = Build("abcdefghijklmnopqrstuvwxyz", _e => {
                _e.TimeMode = TimeMode.Scaled;
                _e.CharactersPerSecond = 1000f;
            });
            yield return WaitFrames(3);
            int _before = _effect.VisibleCount;
            yield return WaitFrames(5);
            Assert.AreEqual(_before, _effect.VisibleCount, "Scaled time must not advance at timeScale 0");

            _effect.TimeMode = TimeMode.Unscaled;
            Assert.AreEqual(TimeMode.Unscaled, _effect.TimeMode);
            yield return WaitFrames(5);
            Assert.Greater(_effect.VisibleCount, _before);
        }

        [UnityTest]
        public IEnumerator Pause_FreezesTypingAndEffects() {
            EffectText _effect = Build(new string('a', 400), _e => {
                _e.TimeMode = TimeMode.Unscaled;
                _e.CharactersPerSecond = 100f;
            });
            Assert.IsFalse(_effect.IsPaused);
            yield return WaitFrames(2);
            _effect.Pause();
            Assert.IsTrue(_effect.IsPaused);
            Assert.IsTrue(_effect.enabled);

            int _frozen = _effect.VisibleCount;
            yield return WaitFrames(10);
            Assert.AreEqual(_frozen, _effect.VisibleCount);
            Assert.AreEqual(EffectTextState.Typing, _effect.State);

            _effect.Resume();
            Assert.IsFalse(_effect.IsPaused);
            yield return WaitFrames(5);
            Assert.Greater(_effect.VisibleCount, _frozen);
        }

        [UnityTest]
        public IEnumerator Pause_WhileHiding_DoesNotFinishHide() {
            settings.DefaultDisappearAsset = FadeOutPreset();
            EffectText _effect = Build("abc", _e => {
                _e.TimeMode = TimeMode.Unscaled;
                _e.HideMode = HideMode.AllAtOnce;
            });
            int _hidden = 0;
            _effect.Hidden += () => _hidden++;
            _effect.Skip();
            _effect.Hide();
            Assert.AreEqual(EffectTextState.Hiding, _effect.State);

            _effect.Pause();
            float _end = Time.realtimeSinceStartup + 0.8f;
            while (Time.realtimeSinceStartup < _end) yield return null;
            Assert.AreEqual(EffectTextState.Hiding, _effect.State);
            Assert.AreEqual(0, _hidden);

            _effect.Resume();
            _end = Time.realtimeSinceStartup + 1.5f;
            while (_effect.State == EffectTextState.Hiding && Time.realtimeSinceStartup < _end) yield return null;
            Assert.AreEqual(EffectTextState.Hidden, _effect.State);
            Assert.AreEqual(1, _hidden);
        }

        [UnityTest]
        public IEnumerator SetEventContext_SelfIsGivenObject() {
            GameObject _other = new GameObject("EventSelf");
            created.Add(_other);
            EffectText _effect = BuildWithEvent();
            _effect.SetEventContext(_other);
            _effect.Tick(1f);
            yield return WaitFrames(3);

            Assert.AreEqual(1, SelfRecordingAction.Runs);
            Assert.AreSame(_other, SelfRecordingAction.LastSelf);
        }

        [UnityTest]
        public IEnumerator SetEventContext_Null_SelfIsEffectText() {
            GameObject _other = new GameObject("EventSelf");
            created.Add(_other);
            EffectText _effect = BuildWithEvent();
            _effect.SetEventContext(_other);
            _effect.SetEventContext(null);
            _effect.Tick(1f);
            yield return WaitFrames(3);

            Assert.AreEqual(1, SelfRecordingAction.Runs);
            Assert.AreSame(_effect.gameObject, SelfRecordingAction.LastSelf);
        }
    }
}
