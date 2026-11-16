using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarryTools.TextEffects.Tests {
    public class TextEffectsSettingsTests {
        // ==================== VARIABLES ===================
        #region Private Variables
        private readonly List<Object> created = new();
        #endregion

        // ==================== SETUP ====================
        #region Setup
        [TearDown]
        public void TearDown() {
            TextEffectsSettings.ClearForTests();
            foreach (Object _object in created) {
                if (_object != null) Object.DestroyImmediate(_object);
            }
            created.Clear();
        }

        private TextEffectPreset MakePreset(string _tag) {
            TextEffectPreset _preset = ScriptableObject.CreateInstance<TextEffectPreset>();
            _preset.name = _tag;
            typeof(TextEffectPreset).GetField("tagName", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_preset, _tag);
            created.Add(_preset);
            return _preset;
        }

        private TypingVoice MakeVoice(string _tag) {
            TypingVoice _voice = ScriptableObject.CreateInstance<TypingVoice>();
            _voice.name = _tag;
            _voice.TagName = _tag;
            created.Add(_voice);
            return _voice;
        }

        private TextEffectsSettings MakeSettings(IEnumerable<TextEffectPreset> _presets, IEnumerable<TypingVoice> _voices) {
            TextEffectsSettings _settings = ScriptableObject.CreateInstance<TextEffectsSettings>();
            created.Add(_settings);
            _settings.PresetList.AddRange(_presets);
            _settings.VoiceList.AddRange(_voices);
            _settings.Invalidate();
            return _settings;
        }
        #endregion

        // ==================== TESTS ====================
        #region Tests
        [Test]
        public void Settings_LookupIgnoresCase() {
            TextEffectPreset _preset = MakePreset("Shake");
            TypingVoice _voice = MakeVoice("Robot");
            TextEffectsSettings _settings = MakeSettings(new[] { _preset }, new[] { _voice });

            Assert.IsTrue(_settings.TryGetPreset("sHAKE", out TextEffectPreset _foundPreset));
            Assert.AreSame(_preset, _foundPreset);
            Assert.IsTrue(_settings.TryGetVoice("ROBOT", out TypingVoice _foundVoice));
            Assert.AreSame(_voice, _foundVoice);
            Assert.IsTrue(_settings.IsPreset("shake"));
            Assert.IsTrue(_settings.IsVoice("robot"));
            Assert.IsFalse(_settings.IsPreset("robot"));
            Assert.IsEmpty(_settings.RegistryIssues);
        }

        [Test]
        public void Settings_ReservedName_IssueNotRegistered() {
            TextEffectsSettings _settings = MakeSettings(new[] { MakePreset("pause"), MakePreset("color") }, new TypingVoice[0]);

            Assert.IsFalse(_settings.TryGetPreset("pause", out _));
            Assert.IsFalse(_settings.TryGetPreset("color", out _));
            Assert.AreEqual(2, _settings.RegistryIssues.Count);
        }

        [Test]
        public void Settings_DuplicateBetweenPresetAndVoice_FirstWins_Issue() {
            TextEffectPreset _preset = MakePreset("echo");
            TypingVoice _voice = MakeVoice("echo");
            TextEffectsSettings _settings = MakeSettings(new[] { _preset }, new[] { _voice });

            Assert.IsTrue(_settings.TryGetPreset("echo", out TextEffectPreset _found));
            Assert.AreSame(_preset, _found);
            Assert.IsFalse(_settings.TryGetVoice("echo", out _));
            Assert.AreEqual(1, _settings.RegistryIssues.Count);
        }

        [Test]
        public void Settings_EmptyOrInvalidName_Issue() {
            TextEffectsSettings _settings = MakeSettings(new[] { MakePreset(""), MakePreset("bad name"), MakePreset("a<b") }, new TypingVoice[0]);

            Assert.AreEqual(3, _settings.RegistryIssues.Count);
        }

        [Test]
        public void Settings_NonAsciiName_IssueNotRegistered() {
            TextEffectsSettings _settings = MakeSettings(new[] { MakePreset("sacudón") }, new[] { MakeVoice("niño") });

            Assert.IsFalse(_settings.TryGetPreset("sacudón", out _));
            Assert.IsFalse(_settings.TryGetVoice("niño", out _));
            Assert.AreEqual(2, _settings.RegistryIssues.Count);
        }

        [Test]
        public void Settings_NameRule_MatchesParser() {
            TextEffectsSettings _settings = MakeSettings(new[] { MakePreset("wave_2-b") }, new TypingVoice[0]);
            ParsedText _parsed = new ParsedText();

            TextParser.Parse("<wave_2-b>x</wave_2-b>", _settings, _parsed);

            Assert.IsTrue(_settings.TryGetPreset("wave_2-b", out _));
            Assert.AreEqual("x", _parsed.TmpText);
        }

        [Test]
        public void Settings_NoResources_EmptyAndWarnsOnce() {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("TextEffectsSettings"));
            TextEffectsSettings _empty = TextEffectsSettings.LoadOrEmpty(() => null);
            created.Add(_empty);

            Assert.IsNotNull(_empty);
            Assert.IsFalse(_empty.TryGetPreset("fade", out _));
            Assert.IsEmpty(_empty.RegistryIssues);
        }

        [Test]
        public void Settings_UseForTests_IsCurrent() {
            TextEffectsSettings _settings = MakeSettings(new TextEffectPreset[0], new TypingVoice[0]);
            TextEffectsSettings.UseForTests(_settings);

            Assert.AreSame(_settings, TextEffectsSettings.Current);
        }
        #endregion
    }
}
