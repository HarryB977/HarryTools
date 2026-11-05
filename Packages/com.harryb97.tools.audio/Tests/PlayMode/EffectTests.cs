using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HarryTools.Audio.Tests {
    public class EffectTests {
        // ==================== VARIABLES ===================
        private const string TestKey = "HarryTools.Audio.Tests.Effect";

        private readonly List<Object> created = new List<Object>();
        private AudioProjectSettings settings;
        private GameObject listener;

        // ==================== SETUP ===================
        [SetUp]
        public void SetUp() {
            PlayerPrefs.DeleteKey(TestKey);
            listener = new GameObject("Test Listener", typeof(AudioListener));
            settings = ScriptableObject.CreateInstance<AudioProjectSettings>();
            settings.MaxVoices = 1;
            settings.PrewarmVoices = 1;
            settings.PersistenceKey = TestKey;
            created.Add(settings);
            Audio.InitializeForTests(settings);
        }

        [TearDown]
        public void TearDown() {
            Audio.ShutdownForTests();
            AudioPlatform.OverrideUsesMixer(null);
            PlayerPrefs.DeleteKey(TestKey);
            if (listener != null) Object.DestroyImmediate(listener);
            for (int _i = 0; _i < created.Count; _i++) {
                if (created[_i] != null) Object.Destroy(created[_i]);
            }
            created.Clear();
        }

        // ==================== HELPERS ===================
        private AudioClip MakeClip(float _seconds) {
            int _samples = Mathf.CeilToInt(44100 * _seconds);
            var _clip = AudioClip.Create("Sine", _samples, 1, 44100, false);
            var _data = new float[_samples];
            for (int _i = 0; _i < _samples; _i++) _data[_i] = 0.05f * Mathf.Sin(2f * Mathf.PI * 440f * _i / 44100f);
            _clip.SetData(_data, 0);
            created.Add(_clip);
            return _clip;
        }

        private SpeakerAsset MakeSpeaker(AudioEffectPreset _defaultEffect = null) {
            var _speaker = ScriptableObject.CreateInstance<SpeakerAsset>();
            if (_defaultEffect != null) _speaker.SetDefaultEffect(_defaultEffect);
            created.Add(_speaker);
            return _speaker;
        }

        private VoiceAsset MakeVoice(SpeakerAsset _speaker, float _seconds) {
            var _voice = ScriptableObject.CreateInstance<VoiceAsset>();
            _voice.SetClips(new[] { new ClipEntry(MakeClip(_seconds)) });
            _voice.SetSpeaker(_speaker);
            created.Add(_voice);
            return _voice;
        }

        private SoundAsset MakeSound(float _seconds) {
            var _sound = ScriptableObject.CreateInstance<SoundAsset>();
            _sound.SetClips(new[] { new ClipEntry(MakeClip(_seconds)) });
            created.Add(_sound);
            return _sound;
        }

        private AudioEffectPreset MakePhone() {
            var _preset = ScriptableObject.CreateInstance<AudioEffectPreset>();
            _preset.LowPass = true;
            _preset.LowPassCutoff = 3000f;
            _preset.HighPass = true;
            _preset.HighPassCutoff = 500f;
            _preset.Distortion = true;
            _preset.DistortionLevel = 0.3f;
            created.Add(_preset);
            return _preset;
        }

        private AudioEmitter MakeEmitter(SpeakerAsset _speaker, AudioEffectPreset _effect) {
            var _go = new GameObject("Emitter");
            var _emitter = _go.AddComponent<AudioEmitter>();
            _emitter.Speaker = _speaker;
            _emitter.Effect = _effect;
            created.Add(_go);
            return _emitter;
        }

        private static AudioVoice VoiceOf(AudioHandle _handle) {
            Assert.IsTrue(Audio.Runtime.Voices.TryGetVoice(_handle, out AudioVoice _voice), "Handle has no voice.");
            return _voice;
        }

        private static IEnumerator WaitFor(Func<bool> _condition, float _timeout) {
            float _end = Time.realtimeSinceStartup + _timeout;
            while (!_condition() && Time.realtimeSinceStartup < _end) yield return null;
        }

        private static void AssertPhone(AudioVoice _voice) {
            Assert.IsTrue(_voice.LowPass.enabled, "Low pass");
            Assert.AreEqual(3000f, _voice.LowPass.cutoffFrequency, 0.5f);
            Assert.IsTrue(_voice.HighPass.enabled, "High pass");
            Assert.AreEqual(500f, _voice.HighPass.cutoffFrequency, 0.5f);
            Assert.IsTrue(_voice.Distortion.enabled, "Distortion");
            Assert.AreEqual(0.3f, _voice.Distortion.distortionLevel, 0.001f);
            Assert.IsFalse(_voice.Echo.enabled, "Echo");
            Assert.IsTrue(_voice.HasEffectLowPass);
            Assert.AreEqual(3000f, _voice.EffectLowPassCutoff, 0.5f);
            Assert.IsTrue(_voice.HasEffectHighPass);
            Assert.AreEqual(500f, _voice.EffectHighPassCutoff, 0.5f);
        }

        private static void AssertNoFilters(AudioVoice _voice) {
            Assert.IsFalse(_voice.LowPass.enabled, "Low pass");
            Assert.IsFalse(_voice.HighPass.enabled, "High pass");
            Assert.IsFalse(_voice.Distortion.enabled, "Distortion");
            Assert.IsFalse(_voice.Echo.enabled, "Echo");
        }

        // ==================== TESTS ===================
        [UnityTest]
        public IEnumerator EmitterEffect_EnablesFilters() {
            SpeakerAsset _speaker = MakeSpeaker();
            MakeEmitter(_speaker, MakePhone());
            var _conversation = ScriptableObject.CreateInstance<ConversationAsset>();
            created.Add(_conversation);
            _conversation.SetLines(new[] {
                new ConversationLine { Voice = MakeVoice(_speaker, 0.1f) },
                new ConversationLine { Voice = MakeVoice(_speaker, 0.1f) },
                new ConversationLine { Voice = MakeVoice(_speaker, 0.1f) }
            });

            int _checked = 0;
            Action<VoiceLineInfo> _check = _info => {
                AssertPhone(VoiceOf(_info.Handle));
                _checked++;
            };
            Audio.Voices.LineStarted += _check;
            ConversationHandle _handle = Audio.Voices.Play(_conversation);
            yield return WaitFor(() => !_handle.IsPlaying, 3f);
            Audio.Voices.LineStarted -= _check;
            Assert.AreEqual(3, _checked);
        }

        [Test]
        public void SpeakerDefaultEffect_UsedWhenEmitterHasNone() {
            SpeakerAsset _speaker = MakeSpeaker(MakePhone());
            AudioEmitter _emitter = MakeEmitter(_speaker, null);
            AssertPhone(VoiceOf(_emitter.Say(MakeVoice(_speaker, 1f))));
        }

        [Test]
        public void EmitterEffect_WinsOverSpeakerDefault() {
            var _echo = ScriptableObject.CreateInstance<AudioEffectPreset>();
            _echo.Echo = true;
            created.Add(_echo);
            SpeakerAsset _speaker = MakeSpeaker(_echo);
            AudioEmitter _emitter = MakeEmitter(_speaker, MakePhone());
            AssertPhone(VoiceOf(_emitter.Say(MakeVoice(_speaker, 1f))));
        }

        [Test]
        public void EmitterPlay_Sound_UsesEffect() {
            AudioEmitter _emitter = MakeEmitter(MakeSpeaker(), MakePhone());
            AssertPhone(VoiceOf(_emitter.Play(MakeSound(1f))));
        }

        [UnityTest]
        public IEnumerator Release_ResetsFilters() {
            SpeakerAsset _speaker = MakeSpeaker();
            AudioEmitter _emitter = MakeEmitter(_speaker, MakePhone());
            AudioHandle _line = _emitter.Say(MakeVoice(_speaker, 0.1f));
            AssertPhone(VoiceOf(_line));
            yield return WaitFor(() => !_line.IsValid, 2f);

            AudioHandle _sound = Audio.Play(MakeSound(1f));
            Assert.AreEqual(_line.VoiceIndex, _sound.VoiceIndex, "The only voice must be reused.");
            AudioVoice _voice = VoiceOf(_sound);
            AssertNoFilters(_voice);
            Assert.IsFalse(_voice.HasEffectLowPass);
            Assert.IsFalse(_voice.HasEffectHighPass);
        }

        [Test]
        public void NoMixer_EffectsNotApplied() {
            AudioPlatform.OverrideUsesMixer(false);
            Audio.InitializeForTests(settings);
            SpeakerAsset _speaker = MakeSpeaker();
            AudioEmitter _emitter = MakeEmitter(_speaker, MakePhone());
            AudioVoice _voice = VoiceOf(_emitter.Say(MakeVoice(_speaker, 1f)));
            AssertNoFilters(_voice);
            Assert.IsFalse(_voice.HasEffectLowPass);
        }
    }
}
