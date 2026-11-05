using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HarryTools.Audio.Tests {
    public class BusMixerTests {
        private const string MixerPath = "Packages/com.harryb97.tools.audio/Runtime/Mixer/HarryTools Audio Mixer.mixer";
        private const string TestKey = "HarryTools.Audio.Tests.Volumes";

        private AudioMixer mixer;
        private AudioProjectSettings settings;

        [SetUp]
        public void SetUp() {
#if UNITY_EDITOR
            mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
#endif
            Assert.IsNotNull(mixer, "Package mixer not found at " + MixerPath);
            settings = ScriptableObject.CreateInstance<AudioProjectSettings>();
            settings.Mixer = mixer;
            AudioPlatform.OverrideUsesMixer(true);
        }

        [TearDown]
        public void TearDown() {
            AudioPlatform.OverrideUsesMixer(null);
            PlayerPrefs.DeleteKey(TestKey);
            if (settings != null) Object.DestroyImmediate(settings);
        }

        [Test]
        public void SetVolume_WritesDbToExposedParameter() {
            var _bus = new BusMixer(settings);
            _bus.SetVolume(AudioBus.Music, 0.5f);
            Assert.IsTrue(mixer.GetFloat("MusicVolume", out float _db));
            Assert.AreEqual(-6.02f, _db, 0.05f);
        }

        [Test]
        public void Mute_ThenUnmute_RestoresVolume() {
            var _bus = new BusMixer(settings);
            _bus.SetVolume(AudioBus.Music, 0.5f);
            _bus.SetMuted(AudioBus.Music, true);
            mixer.GetFloat("MusicVolume", out float _muted);
            Assert.AreEqual(-80f, _muted, 0.01f);
            Assert.IsTrue(_bus.IsMuted(AudioBus.Music));
            _bus.SetMuted(AudioBus.Music, false);
            mixer.GetFloat("MusicVolume", out float _restored);
            Assert.AreEqual(-6.02f, _restored, 0.05f);
            Assert.AreEqual(0.5f, _bus.GetVolume(AudioBus.Music), 0.0001f);
        }

        [Test]
        public void DuckOffset_AddsToVolume() {
            var _bus = new BusMixer(settings);
            _bus.SetVolume(AudioBus.Music, 0.5f);
            _bus.SetDuckOffsetDb(AudioBus.Music, -6f);
            mixer.GetFloat("MusicVolume", out float _db);
            Assert.AreEqual(-12.04f, _db, 0.05f);
        }

        [Test]
        public void DuckOffset_PositiveIsClampedToZero() {
            var _bus = new BusMixer(settings);
            _bus.SetDuckOffsetDb(AudioBus.Music, 6f);
            Assert.AreEqual(0f, _bus.GetDuckOffsetDb(AudioBus.Music));
            mixer.GetFloat("MusicVolume", out float _db);
            Assert.AreEqual(0f, _db, 0.01f);
            Assert.AreEqual(1f, _bus.GetSourceMultiplier(AudioBus.Music), 0.0001f);
        }

        [Test]
        public void DuckOffset_NaN_IsIgnored() {
            var _bus = new BusMixer(settings);
            _bus.SetDuckOffsetDb(AudioBus.Music, -6f);
            _bus.SetDuckOffsetDb(AudioBus.Music, float.NaN);
            Assert.AreEqual(0f, _bus.GetDuckOffsetDb(AudioBus.Music));
            mixer.GetFloat("MusicVolume", out float _db);
            Assert.IsFalse(float.IsNaN(_db));
        }

        [Test]
        public void Volume_Zero_NeverWritesInfinity() {
            var _bus = new BusMixer(settings);
            _bus.SetVolume(AudioBus.SFX, 0f);
            mixer.GetFloat("SFXVolume", out float _db);
            Assert.AreEqual(-80f, _db, 0.01f);
        }

        [Test]
        public void NoMixer_SourceMultiplier_IncludesMaster() {
            AudioPlatform.OverrideUsesMixer(false);
            var _bus = new BusMixer(settings);
            _bus.SetVolume(AudioBus.Master, 0.5f);
            _bus.SetVolume(AudioBus.Music, 0.5f);
            Assert.AreEqual(0.25f, _bus.GetSourceMultiplier(AudioBus.Music), 0.0001f);
            Assert.AreEqual(0.5f, _bus.GetSourceMultiplier(AudioBus.Master), 0.0001f);
            _bus.SetMuted(AudioBus.Master, true);
            Assert.AreEqual(0f, _bus.GetSourceMultiplier(AudioBus.Music), 0.0001f);
        }

        [Test]
        public void NoMixerAssigned_BehavesLikeNoMixerPath() {
            settings.Mixer = null;
            var _bus = new BusMixer(settings);
            _bus.SetVolume(AudioBus.Music, 0.5f);
            _bus.SetDuckOffsetDb(AudioBus.Music, -6f);
            Assert.AreEqual(0.5f * 0.5012f, _bus.GetSourceMultiplier(AudioBus.Music), 0.001f);
        }

        [Test]
        public void CaptureRestore_RoundTrip() {
            var _a = new BusMixer(settings);
            _a.SetVolume(AudioBus.Voice, 0.3f);
            _a.SetMuted(AudioBus.UI, true);
            AudioVolumeData _data = _a.Capture();

            var _b = new BusMixer(settings);
            _b.Restore(_data);
            Assert.AreEqual(0.3f, _b.GetVolume(AudioBus.Voice), 0.0001f);
            Assert.IsTrue(_b.IsMuted(AudioBus.UI));
            Assert.IsFalse(_b.IsMuted(AudioBus.Music));
        }

        [UnityTest]
        public System.Collections.IEnumerator PlayerPrefsStore_SaveLoad_RoundTrip() => UniTask.ToCoroutine(async () => {
            var _store = new PlayerPrefsAudioStore(TestKey);
            AudioVolumeData? _none = await _store.Load();
            Assert.IsFalse(_none.HasValue);

            var _bus = new BusMixer(settings);
            _bus.SetVolume(AudioBus.SFX, 0.4f);
            _bus.SetMuted(AudioBus.Ambience, true);
            await _store.Save(_bus.Capture());

            AudioVolumeData? _loaded = await _store.Load();
            Assert.IsTrue(_loaded.HasValue);
            var _other = new BusMixer(settings);
            _other.Restore(_loaded.Value);
            Assert.AreEqual(0.4f, _other.GetVolume(AudioBus.SFX), 0.0001f);
            Assert.IsTrue(_other.IsMuted(AudioBus.Ambience));
        });
    }
}
