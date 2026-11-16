using System;
using System.Collections;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using HarryTools.Conditions;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarryTools.TextEffects.Tests {
    public class TextActionsTests {
        // ==================== VARIABLES ===================
        private GameObject canvasObject;
        private TextEffectsSettings settings;

        // ==================== SETUP ===================
        [SetUp]
        public void SetUp() {
            settings = ScriptableObject.CreateInstance<TextEffectsSettings>();
            settings.hideFlags = HideFlags.HideAndDontSave;
            TextEffectsSettings.UseForTests(settings);
            canvasObject = new GameObject("TextActionsTestCanvas", typeof(Canvas));
        }

        [TearDown]
        public void TearDown() {
            if (canvasObject != null) UnityEngine.Object.DestroyImmediate(canvasObject);
            TextEffectsSettings.ClearForTests();
            UnityEngine.Object.DestroyImmediate(settings);
        }

        private EffectText Build(string _text, float _charactersPerSecond = 100f) {
            GameObject _go = new GameObject("Text");
            _go.SetActive(false);
            _go.transform.SetParent(canvasObject.transform, false);
            _go.AddComponent<TextMeshProUGUI>();
            EffectText _effect = _go.AddComponent<EffectText>();
            _effect.PlainText = _text;
            _effect.CharactersPerSecond = _charactersPerSecond;
            _effect.ShowOnEnable = false;
            _go.SetActive(true);
            return _effect;
        }

        private void UseDisappearPreset() {
            FadeTransition _effect = new FadeTransition();
            Assert.IsTrue(EffectParamBinder.TryApply(_effect, "direction", "Out", out string _e1), _e1);
            Assert.IsTrue(EffectParamBinder.TryApply(_effect, "duration", "0.3", out string _e2), _e2);
            TextEffectPreset _preset = ScriptableObject.CreateInstance<TextEffectPreset>();
            _preset.hideFlags = HideFlags.HideAndDontSave;
            typeof(TextEffectPreset).GetField("tagName", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_preset, "fadeout");
            typeof(TextEffectPreset).GetField("effect", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_preset, _effect);
            settings.DefaultDisappearAsset = _preset;
        }

        private static int HandlerCount(EffectText _effect, string _event) {
            FieldInfo _field = typeof(EffectText).GetField(_event, BindingFlags.NonPublic | BindingFlags.Instance);
            Delegate _delegate = (Delegate)_field.GetValue(_effect);
            return _delegate == null ? 0 : _delegate.GetInvocationList().Length;
        }

        private static TargetComponent<EffectText> Target(EffectText _effect) {
            return new TargetComponent<EffectText>(TargetSource.Custom, _effect);
        }

        private static LogicContext Context(EffectText _effect) => new LogicContext(_effect.gameObject);

        // ==================== TESTS ===================
        [Test]
        public void Show_StartsTyping() {
            EffectText _effect = Build("hello");
            new ShowTextAction(Target(_effect), false).Run(Context(_effect), CancellationToken.None);
            Assert.AreEqual(EffectTextState.Typing, _effect.State);
        }

        [UnityTest]
        public IEnumerator Show_Wait_CompletesAfterTyping() => UniTask.ToCoroutine(async () => {
            EffectText _effect = Build("hello");
            await new ShowTextAction(Target(_effect), true).Run(Context(_effect), CancellationToken.None);
            Assert.AreEqual(EffectTextState.Complete, _effect.State);
        });

        [UnityTest]
        public IEnumerator Hide_Wait_CompletesWhenHidden() => UniTask.ToCoroutine(async () => {
            UseDisappearPreset();
            EffectText _effect = Build("hello");
            _effect.Show();
            _effect.Skip();
            Assert.AreEqual(EffectTextState.Complete, _effect.State);
            UniTask _wait = new HideTextAction(Target(_effect), true).Run(Context(_effect), CancellationToken.None);
            Assert.AreEqual(EffectTextState.Hiding, _effect.State);
            await _wait;
            Assert.AreEqual(EffectTextState.Hidden, _effect.State);
        });

        [Test]
        public void Skip_Completes() {
            EffectText _effect = Build("hello", 1f);
            _effect.Show();
            Assert.AreEqual(EffectTextState.Typing, _effect.State);
            new SkipTextAction(Target(_effect)).Run(Context(_effect), CancellationToken.None);
            Assert.AreEqual(EffectTextState.Complete, _effect.State);
        }

        [Test]
        public void Continue_ResumesWaitInput() {
            EffectText _effect = Build("ab<waitinput>cd", 1f);
            _effect.Show();
            _effect.Skip();
            Assert.AreEqual(EffectTextState.WaitingInput, _effect.State);
            new ContinueTextAction(Target(_effect)).Run(Context(_effect), CancellationToken.None);
            Assert.AreEqual(EffectTextState.Typing, _effect.State);
        }

        [UnityTest]
        public IEnumerator Cancelled_WhileWaiting_Throws() => UniTask.ToCoroutine(async () => {
            EffectText _effect = Build("hello", 0.1f);
            using CancellationTokenSource _cts = new CancellationTokenSource();
            UniTask _task = new ShowTextAction(Target(_effect), true).Run(Context(_effect), _cts.Token);
            await UniTask.Yield();
            _cts.Cancel();
            bool _threw = false;
            try {
                await _task;
            } catch (OperationCanceledException) {
                _threw = true;
            }
            Assert.IsTrue(_threw);
            Assert.AreEqual(0, HandlerCount(_effect, "TypingCompleted"));
            Assert.AreEqual(0, HandlerCount(_effect, "Hidden"));
        });

        [Test]
        public void Show_NullTarget_NoOp() {
            GameObject _go = new GameObject("Empty");
            try {
                Assert.DoesNotThrow(() => new ShowTextAction(new TargetComponent<EffectText>(TargetSource.Self), true).Run(new LogicContext(_go), CancellationToken.None));
                Assert.DoesNotThrow(() => new HideTextAction(new TargetComponent<EffectText>(TargetSource.Self), true).Run(new LogicContext(_go), CancellationToken.None));
            } finally {
                UnityEngine.Object.DestroyImmediate(_go);
            }
        }

        [Test]
        public void HideWait_AlreadyHidden_ReturnsImmediately() {
            EffectText _effect = Build("hello");
            UniTask _task = new HideTextAction(Target(_effect), true).Run(Context(_effect), CancellationToken.None);
            Assert.AreEqual(UniTaskStatus.Succeeded, _task.Status);
        }

        [UnityTest]
        public IEnumerator ShowWait_InstantlyComplete_Returns() => UniTask.ToCoroutine(async () => {
            EffectText _effect = Build("");
            await new ShowTextAction(Target(_effect), true).Run(Context(_effect), CancellationToken.None);
            Assert.AreEqual(EffectTextState.Complete, _effect.State);
        });

        [UnityTest]
        public IEnumerator ShowWait_TargetDisabledMidTyping_Returns() => UniTask.ToCoroutine(async () => {
            EffectText _effect = Build("hello", 0.1f);
            UniTask _task = new ShowTextAction(Target(_effect), true).Run(Context(_effect), CancellationToken.None);
            await UniTask.Yield();
            Assert.AreEqual(UniTaskStatus.Pending, _task.Status);
            _effect.enabled = false;
            await UniTask.Yield();
            await UniTask.Yield();
            Assert.AreEqual(UniTaskStatus.Succeeded, _task.Status);
        });

        [UnityTest]
        public IEnumerator ShowWait_TargetDestroyed_Returns() => UniTask.ToCoroutine(async () => {
            EffectText _effect = Build("hello", 0.1f);
            UniTask _task = new ShowTextAction(Target(_effect), true).Run(Context(_effect), CancellationToken.None);
            await UniTask.Yield();
            UnityEngine.Object.Destroy(_effect.gameObject);
            await UniTask.Yield();
            await UniTask.Yield();
            Assert.AreEqual(UniTaskStatus.Succeeded, _task.Status);
        });

        [UnityTest]
        public IEnumerator HideWait_TargetDestroyed_Returns() => UniTask.ToCoroutine(async () => {
            UseDisappearPreset();
            EffectText _effect = Build("hello");
            _effect.Show();
            _effect.Skip();
            UniTask _task = new HideTextAction(Target(_effect), true).Run(Context(_effect), CancellationToken.None);
            Assert.AreEqual(UniTaskStatus.Pending, _task.Status);
            UnityEngine.Object.Destroy(_effect.gameObject);
            await UniTask.Yield();
            await UniTask.Yield();
            Assert.AreEqual(UniTaskStatus.Succeeded, _task.Status);
        });
    }
}
