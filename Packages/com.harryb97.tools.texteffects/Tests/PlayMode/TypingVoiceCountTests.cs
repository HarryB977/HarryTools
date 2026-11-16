using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace HarryTools.TextEffects.Tests {
    public class TypingVoiceCountTests {
        // ==================== VARIABLES ===================
        private GameObject canvasObject;
        private TextEffectsSettings settings;
        private readonly List<Object> created = new List<Object>();
        private readonly List<TypingVoice> played = new List<TypingVoice>();

        // ==================== SETUP ===================
        [SetUp]
        public void SetUp() {
            played.Clear();
            settings = ScriptableObject.CreateInstance<TextEffectsSettings>();
            settings.hideFlags = HideFlags.HideAndDontSave;
            TextEffectsSettings.UseForTests(settings);
            EffectText.PlayedForTests = _voice => played.Add(_voice);
            canvasObject = new GameObject("TextEffectsTestCanvas", typeof(Canvas));
        }

        [TearDown]
        public void TearDown() {
            EffectText.PlayedForTests = null;
            if (canvasObject != null) Object.DestroyImmediate(canvasObject);
            for (int _i = 0; _i < created.Count; _i++) {
                if (created[_i] != null) Object.DestroyImmediate(created[_i]);
            }
            created.Clear();
            TextEffectsSettings.ClearForTests();
            Object.DestroyImmediate(settings);
        }

        private TypingVoice Voice(string _tag, int _everyN) {
            TypingVoice _voice = ScriptableObject.CreateInstance<TypingVoice>();
            created.Add(_voice);
            _voice.TagName = _tag;
            _voice.EveryNCharacters = _everyN;
            settings.VoiceList.Add(_voice);
            settings.Invalidate();
            return _voice;
        }

        private EffectText Build(string _text, TypingVoice _default) {
            GameObject _go = new GameObject("Text");
            _go.SetActive(false);
            _go.transform.SetParent(canvasObject.transform, false);
            _go.AddComponent<TextMeshProUGUI>();
            EffectText _effect = _go.AddComponent<EffectText>();
            _effect.PlainText = _text;
            _effect.CharactersPerSecond = 100f;
            _effect.DefaultVoice = _default;
            _go.SetActive(true);
            return _effect;
        }

        // ==================== TESTS ===================
        [Test]
        public void EveryN2_PlaysAandC_SkipsSpaces() {
            TypingVoice _voice = Voice("v", 2);
            List<char> _chars = new List<char>();
            EffectText _effect = Build("ab cd", _voice);
            _effect.CharacterShown += _args => { if (_args.Character != ' ') _chars.Add(_args.Character); };

            _effect.Tick(1f);

            Assert.AreEqual(2, played.Count);
            Assert.AreEqual(4, _chars.Count);
            Assert.AreSame(_voice, played[0]);
        }

        [Test]
        public void EveryN2_WithoutSubscribers_StillPlays() {
            EffectText _effect = Build("ab cd", Voice("v", 2));
            _effect.Tick(1f);
            Assert.AreEqual(2, played.Count);
        }

        [Test]
        public void VoiceChange_ResetsCounter() {
            Voice("first", 2);
            Voice("second", 2);
            // 'a' plays, 'b' does not; the change resets so 'c' plays again
            EffectText _effect = Build("ab<voice=second>cd", settings.VoiceList[0]);
            _effect.Tick(1f);

            Assert.AreEqual(2, played.Count);
            Assert.AreSame(settings.VoiceList[0], played[0]);
            Assert.AreSame(settings.VoiceList[1], played[1]);
        }

        [Test]
        public void Skipped_DoesNotPlay() {
            EffectText _effect = Build("abcd", Voice("v", 1));
            _effect.Skip();
            Assert.AreEqual(0, played.Count);
        }

        [Test]
        public void NoVoice_DoesNotPlay() {
            EffectText _effect = Build("abcd", null);
            _effect.Tick(1f);
            Assert.AreEqual(0, played.Count);
        }

        [Test]
        public void PreviewMode_DoesNotPlay() {
            EffectText _effect = Build("abcd", Voice("v", 1));
            _effect.PreviewMode = true;
            _effect.Tick(1f);
            Assert.AreEqual(0, played.Count);
        }
    }
}
