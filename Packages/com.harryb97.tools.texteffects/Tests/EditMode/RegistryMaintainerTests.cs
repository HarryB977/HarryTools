using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using HarryTools.TextEffects.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

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
            TextEffectsSettings.ClearForTests();
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

        /// <summary> Makes Current hold the empty instance used when no settings asset exists </summary>
        private TextEffectsSettings CacheFallback() {
            LogAssert.Expect(LogType.Warning, new Regex("TextEffectsSettings"));
            TextEffectsSettings _fallback = TextEffectsSettings.LoadOrEmpty(() => null);
            created.Add(_fallback);
            TextEffectsSettings.UseForTests(_fallback);
            return _fallback;
        }

        private static TextEffectsSettings CachedCurrent() {
            return (TextEffectsSettings)typeof(TextEffectsSettings).GetField("current", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
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

        [Test]
        public void GetOrCreate_DropsCachedFallback() {
            TextEffectsSettingsLocator.OverrideForTests = Make<TextEffectsSettings>();
            CacheFallback();

            TextEffectsSettingsLocator.GetOrCreate();

            Assert.IsNull(CachedCurrent());
        }

        [Test]
        public void GetOrCreate_KeepsCachedAsset() {
            TextEffectsSettingsLocator.OverrideForTests = Make<TextEffectsSettings>();
            TextEffectsSettings _real = Make<TextEffectsSettings>();
            TextEffectsSettings.UseForTests(_real);

            TextEffectsSettingsLocator.GetOrCreate();

            Assert.AreSame(_real, CachedCurrent());
        }

        [Test]
        public void SettingsImport_DropsCachedFallback() {
            const string _folder = "Assets/__TextEffectsTests__";
            const string _settingsPath = _folder + "/ImportedSettings.asset";
            const string _presetPath = _folder + "/ImportedPreset.asset";
            TextEffectsSettingsLocator.OverrideForTests = Make<TextEffectsSettings>();
            bool _createdFolder = !AssetDatabase.IsValidFolder(_folder);
            if (_createdFolder) AssetDatabase.CreateFolder("Assets", "__TextEffectsTests__");
            try {
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<TextEffectsSettings>(), _settingsPath);
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<TextEffectPreset>(), _presetPath);

                TextEffectsSettings _fallback = CacheFallback();
                TextEffectsRegistryMaintainer.ForgetFallbackOnSettingsImport(new[] { _presetPath });
                Assert.AreSame(_fallback, CachedCurrent());

                TextEffectsRegistryMaintainer.ForgetFallbackOnSettingsImport(new[] { _presetPath, _settingsPath });
                Assert.IsNull(CachedCurrent());
            } finally {
                AssetDatabase.DeleteAsset(_settingsPath);
                AssetDatabase.DeleteAsset(_presetPath);
                if (_createdFolder) AssetDatabase.DeleteAsset(_folder);
            }
        }
        #endregion
    }
}
