using System;
using System.Collections;
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
            EffectText _effect = Build("hello");
            _effect.Show();
            _effect.Skip();
            Assert.AreEqual(EffectTextState.Complete, _effect.State);
            await new HideTextAction(Target(_effect), true).Run(Context(_effect), CancellationToken.None);
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
        });
    }
}
