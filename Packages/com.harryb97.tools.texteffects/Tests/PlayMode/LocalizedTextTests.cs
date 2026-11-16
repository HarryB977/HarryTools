using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace HarryTools.TextEffects.Tests {
    public class LocalizedTextTests {
        // ==================== VARIABLES ===================
        private GameObject canvasObject;
        private TextEffectsSettings settings;

        // ==================== SETUP ===================
        [SetUp]
        public void SetUp() {
            settings = ScriptableObject.CreateInstance<TextEffectsSettings>();
            settings.hideFlags = HideFlags.HideAndDontSave;
            TextEffectsSettings.UseForTests(settings);
            canvasObject = new GameObject("TextEffectsTestCanvas", typeof(Canvas));
        }

        [TearDown]
        public void TearDown() {
            if (canvasObject != null) Object.DestroyImmediate(canvasObject);
            TextEffectsSettings.ClearForTests();
            Object.DestroyImmediate(settings);
        }

        private EffectText Build(string _text, bool _showOnEnable = true) {
            GameObject _go = new GameObject("Text");
            _go.SetActive(false);
            _go.transform.SetParent(canvasObject.transform, false);
            _go.AddComponent<TextMeshProUGUI>();
            EffectText _effect = _go.AddComponent<EffectText>();
            _effect.PlainText = _text;
            _effect.CharactersPerSecond = 100f;
            _effect.ShowOnEnable = _showOnEnable;
            _go.SetActive(true);
            return _effect;
        }

        // ==================== TESTS ===================
        [Test]
        public void LocaleChange_WhileTyping_Restarts_NoRepeatedEvents() {
            EffectText _effect = Build("<event=a>xx<event=b>yy");
            List<string> _reached = new List<string>();
            _effect.EventReached += _reached.Add;

            _effect.Tick(0.015f);
            CollectionAssert.AreEqual(new[] { "a" }, _reached);

            _effect.ApplyLocalizedText("<event=a>XX<event=b>YY");
            Assert.AreEqual(EffectTextState.Typing, _effect.State);
            Assert.AreEqual(0, _effect.VisibleCount);
            Assert.AreEqual("<event=a>XX<event=b>YY", _effect.Text);

            _effect.Tick(1f);
            CollectionAssert.AreEqual(new[] { "a", "b" }, _reached);
            Assert.AreEqual(EffectTextState.Complete, _effect.State);
        }

        [Test]
        public void LocaleChange_WhenComplete_ShowsComplete_NoEvents() {
            EffectText _effect = Build("<event=a>xx");
            _effect.Tick(1f);
            Assert.AreEqual(EffectTextState.Complete, _effect.State);

            List<string> _reached = new List<string>();
            _effect.EventReached += _reached.Add;
            _effect.ApplyLocalizedText("<event=a>yyyy");

            Assert.AreEqual(EffectTextState.Complete, _effect.State);
            Assert.AreEqual(4, _effect.VisibleCount);
            Assert.AreEqual("<event=a>yyyy", _effect.Text);
            _effect.Tick(1f);
            Assert.IsEmpty(_reached);
        }

        [Test]
        public void LocaleChange_WhenHidden_StaysHidden() {
            EffectText _effect = Build("xx", false);
            Assert.AreEqual(EffectTextState.Hidden, _effect.State);

            _effect.ApplyLocalizedText("yyyy");

            Assert.AreEqual(EffectTextState.Hidden, _effect.State);
            Assert.AreEqual(0, _effect.VisibleCount);
            Assert.AreEqual("yyyy", _effect.Text);
        }
    }
}
