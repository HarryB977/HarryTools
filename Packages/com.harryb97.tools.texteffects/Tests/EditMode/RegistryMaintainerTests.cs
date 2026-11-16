using System.Collections.Generic;
using HarryTools.TextEffects.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HarryTools.TextEffects.Tests {
    public class RegistryMaintainerTests {
        // ==================== VARIABLES ===================
        #region Private Variables
        private readonly List<Object> created = new();
        #endregion

        // ==================== SETUP ====================
        #region Setup
        [TearDown]
        public void TearDown() {
            TextEffectsSettingsLocator.OverrideForTests = null;
            foreach (Object _object in created) {
                if (_object != null) Object.DestroyImmediate(_object);
            }
            created.Clear();
        }

        private T Make<T>() where T : ScriptableObject {
            T _asset = ScriptableObject.CreateInstance<T>();
            created.Add(_asset);
            return _asset;
        }
        #endregion

        // ==================== TESTS ====================
        #region Tests
        [Test]
        public void Maintainer_Sync_AddsNewAndRemovesDeleted() {
            TextEffectsSettings _settings = Make<TextEffectsSettings>();
            TextEffectPreset _kept = Make<TextEffectPreset>();
            TextEffectPreset _removed = Make<TextEffectPreset>();
            TextEffectPreset _added = Make<TextEffectPreset>();
            TypingVoice _voice = Make<TypingVoice>();
            _settings.PresetList.Add(_kept);
            _settings.PresetList.Add(_removed);
            _settings.PresetList.Add(null);

            TextEffectsRegistryMaintainer.Sync(_settings, new[] { _kept, _added }, new[] { _voice });

            CollectionAssert.AreEqual(new[] { _kept, _added }, _settings.PresetList);
            CollectionAssert.AreEqual(new[] { _voice }, _settings.VoiceList);
        }

        [Test]
        public void Maintainer_Sync_Undo() {
            TextEffectsSettings _settings = Make<TextEffectsSettings>();
            TextEffectPreset _preset = Make<TextEffectPreset>();

            Undo.IncrementCurrentGroup();
            TextEffectsRegistryMaintainer.Sync(_settings, new[] { _preset }, new TypingVoice[0]);
            Assert.AreEqual(1, _settings.PresetList.Count);

            Undo.PerformUndo();
            Assert.AreEqual(0, _settings.PresetList.Count);
        }
        #endregion
    }
}
