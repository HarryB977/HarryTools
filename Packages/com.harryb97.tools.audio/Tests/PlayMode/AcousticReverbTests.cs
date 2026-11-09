using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HarryTools.Audio.Tests {
    public class AcousticReverbTests {
        // ==================== FAKES ===================
        private sealed class ReverbModule : IAcousticModule {
            public int OrderValue;
            public bool Write = true;
            public float Wet = 0.5f;
            public float Decay = 3f;
            public float Ratio = 0.7f;
            public float Delay = 0.04f;

            public int Order => OrderValue;
            public float UpdateInterval => 0f;

            public void Evaluate(ReadOnlySpan<AcousticQuery> _queries, Span<AcousticResult> _results) {
                if (!Write) return;
                for (int _i = 0; _i < _queries.Length; _i++) {
                    AcousticResult _r = _results[_i];
                    _r.HasReverb = true;
                    _r.Reverb = new AcousticReverb { Wet = Wet, DecayTime = Decay, HighFrequencyRatio = Ratio, ReflectionsDelay = Delay };
                    _results[_i] = _r;
                }
            }
        }

        private sealed class SilentModule : IAcousticModule {
            public int Order => 0;
            public float UpdateInterval => 0f;
            public void Evaluate(ReadOnlySpan<AcousticQuery> _queries, Span<AcousticResult> _results) { }
        }

        // ==================== VARIABLES ===================
        private const string TestKey = "HarryTools.Audio.Tests.AcousticReverb";

        private readonly System.Collections.Generic.List<Object> created = new System.Collections.Generic.List<Object>();
        private readonly System.Collections.Generic.List<IAcousticModule> registered = new System.Collections.Generic.List<IAcousticModule>();
        private GameObject listener;
        private SoundAsset sound;

        // ==================== SETUP ===================
        [SetUp]
        public void SetUp() {
            PlayerPrefs.DeleteKey(TestKey);
            listener = new GameObject("Test Listener", typeof(AudioListener));
            var _settings = ScriptableObject.CreateInstance<AudioProjectSettings>();
            _settings.MaxVoices = 8;
            _settings.PrewarmVoices = 8;
            _settings.MaxSimulated = 8;
            _settings.PersistenceKey = TestKey;
            created.Add(_settings);
            Audio.InitializeForTests(_settings);

            int _samples = 44100 * 4;
            var _clip = AudioClip.Create("Sine", _samples, 1, 44100, false);
            var _data = new float[_samples];
            for (int _i = 0; _i < _samples; _i++) _data[_i] = 0.02f * Mathf.Sin(2f * Mathf.PI * 440f * _i / 44100f);
            _clip.SetData(_data, 0);
            created.Add(_clip);

            sound = ScriptableObject.CreateInstance<SoundAsset>();
            sound.SetClips(new[] { new ClipEntry(_clip) });
            sound.SetSpatial(SpatialSettings.Default3D);
            created.Add(sound);
        }

        [TearDown]
        public void TearDown() {
            for (int _i = 0; _i < registered.Count; _i++) Audio.UnregisterModule(registered[_i]);
            registered.Clear();
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
        private T Register<T>(T _module) where T : IAcousticModule {
            Audio.RegisterModule(_module);
            registered.Add(_module);
            return _module;
        }

        private static AudioVoice VoiceOf(AudioHandle _handle) {
            Assert.IsTrue(Audio.Runtime.Voices.TryGetVoice(_handle, out AudioVoice _voice), "Handle has no voice.");
            return _voice;
        }

        private static IEnumerator Seconds(float _seconds) {
            float _end = Time.realtimeSinceStartup + _seconds;
            while (Time.realtimeSinceStartup < _end) yield return null;
        }

        private static int CountAllocations(Action _action) {
            Recorder _recorder = Recorder.Get("GC.Alloc");
            _recorder.FilterToCurrentThread();
            _recorder.enabled = true;
            _action();
            _recorder.enabled = false;
            return _recorder.sampleBlockCount;
        }

        // ==================== TESTS ===================
        [UnityTest]
        public IEnumerator NoReverbModule_FilterStaysOff() {
            Register(new SilentModule());
            AudioVoice _voice = VoiceOf(Audio.Play(sound, new Vector3(2f, 0f, 0f)));
            Assert.IsNotNull(_voice.Reverb);
            Assert.AreEqual(AudioReverbPreset.User, _voice.Reverb.reverbPreset);
            yield return Seconds(0.4f);
            Assert.IsFalse(_voice.Reverb.enabled);
            Assert.IsFalse(_voice.Acoustic.Active);
        }

        [UnityTest]
        public IEnumerator Reverb_EnablesFilter_AppliesDecay() {
            Register(new ReverbModule { Wet = 0.5f, Decay = 3f, Ratio = 0.7f, Delay = 0.04f });
            AudioVoice _voice = VoiceOf(Audio.Play(sound, new Vector3(2f, 0f, 0f)));
            yield return Seconds(0.8f);
            AudioReverbFilter _filter = _voice.Reverb;
            Assert.IsTrue(_filter.enabled);
            Assert.AreEqual(AudioReverbPreset.User, _filter.reverbPreset);
            Assert.AreEqual(3f, _filter.decayTime, 0.05f);
            Assert.AreEqual(0.7f, _filter.decayHFRatio, 0.02f);
            Assert.AreEqual(0.04f, _filter.reflectionsDelay, 0.002f);
            Assert.AreEqual(2000f * Mathf.Log10(0.5f), _filter.reverbLevel, 30f);
            Assert.AreEqual(0f, _filter.dryLevel, 0.01f);
            Assert.IsTrue(_voice.Acoustic.Active);
        }

        [UnityTest]
        public IEnumerator Reverb_LastModuleInOrderWins() {
            Register(new ReverbModule { OrderValue = 0, Wet = 0.2f, Decay = 1f });
            Register(new ReverbModule { OrderValue = 5, Wet = 0.8f, Decay = 6f });
            Register(new ReverbModule { OrderValue = 2, Wet = 0.4f, Decay = 2f });
            AudioVoice _voice = VoiceOf(Audio.Play(sound, new Vector3(2f, 0f, 0f)));
            yield return Seconds(0.8f);
            Assert.AreEqual(6f, _voice.Reverb.decayTime, 0.1f);
            Assert.AreEqual(2000f * Mathf.Log10(0.8f), _voice.Reverb.reverbLevel, 30f);
        }

        [UnityTest]
        public IEnumerator Reverb_LastModuleInOrderWins_IgnoresModulesWithoutReverb() {
            Register(new ReverbModule { OrderValue = 0, Wet = 0.8f, Decay = 6f });
            Register(new ReverbModule { OrderValue = 5, Write = false });
            AudioVoice _voice = VoiceOf(Audio.Play(sound, new Vector3(2f, 0f, 0f)));
            yield return Seconds(0.8f);
            Assert.AreEqual(6f, _voice.Reverb.decayTime, 0.1f);
        }

        [UnityTest]
        public IEnumerator Reverb_IsSmoothed() {
            Register(new ReverbModule { Wet = 1f });
            AudioVoice _voice = VoiceOf(Audio.Play(sound, new Vector3(2f, 0f, 0f)));
            Assert.AreEqual(0f, _voice.Acoustic.Wet, 0.0001f);
            yield return null;
            Assert.Less(_voice.Acoustic.Wet, 0.9f, "The wet must not jump to the target in the first tick.");
            yield return Seconds(1f);
            Assert.AreEqual(1f, _voice.Acoustic.Wet, 0.01f);
            Assert.AreEqual(0f, _voice.Reverb.reverbLevel, 10f);
        }

        [UnityTest]
        public IEnumerator ReverbStops_DriftsToOff() {
            ReverbModule _module = Register(new ReverbModule { Wet = 0.6f });
            AudioVoice _voice = VoiceOf(Audio.Play(sound, new Vector3(2f, 0f, 0f)));
            yield return Seconds(0.6f);
            Assert.IsTrue(_voice.Reverb.enabled);

            _module.Write = false;
            yield return null;
            yield return null;
            Assert.Greater(_voice.Acoustic.Wet, 0f, "The wet must fade, not cut.");
            yield return Seconds(1.5f);
            Assert.AreEqual(0f, _voice.Acoustic.Wet, 0.0001f);
            Assert.IsFalse(_voice.Reverb.enabled);
            Assert.IsFalse(_voice.Acoustic.Active);
        }

        [UnityTest]
        public IEnumerator ReleasedVoice_ResetsReverb() {
            Register(new ReverbModule { Wet = 0.9f, Decay = 8f, Ratio = 1.5f, Delay = 0.2f });
            AudioHandle _handle = Audio.Play(sound, new Vector3(2f, 0f, 0f));
            AudioVoice _voice = VoiceOf(_handle);
            yield return Seconds(0.6f);
            Assert.IsTrue(_voice.Reverb.enabled);

            _handle.Stop(0f);
            Assert.IsFalse(_voice.IsActive);
            AudioReverbFilter _filter = _voice.Reverb;
            Assert.IsFalse(_filter.enabled);
            Assert.AreEqual(0f, _voice.Acoustic.Wet, 0.0001f);
            Assert.AreEqual(AudioReverbPreset.User, _filter.reverbPreset);
            Assert.AreEqual(1f, _filter.decayTime, 0.001f);
            Assert.AreEqual(0.5f, _filter.decayHFRatio, 0.001f);
            Assert.AreEqual(0f, _filter.reflectionsDelay, 0.001f);
            Assert.AreEqual(-10000f, _filter.reverbLevel, 0.01f);
        }

        [UnityTest]
        public IEnumerator WebGL_NoReverb() {
            AudioPlatform.OverrideUsesMixer(false);
            Register(new ReverbModule { Wet = 1f });
            AudioVoice _voice = VoiceOf(Audio.Play(sound, new Vector3(2f, 0f, 0f)));
            yield return Seconds(0.4f);
            Assert.IsFalse(_voice.Reverb.enabled);
            Assert.AreEqual(0f, _voice.Acoustic.Wet, 0.0001f);
        }

        [UnityTest]
        public IEnumerator Reverb_ZeroGC() {
            Register(new ReverbModule { Wet = 0.5f });
            for (int _i = 0; _i < 4; _i++) Audio.Play(sound, new Vector3(2f + _i, 0f, 0f));
            yield return Seconds(0.4f);
            AudioRuntime _runtime = Audio.Runtime;
            Action _tick = () => { for (int _i = 0; _i < 100; _i++) _runtime.Acoustics.Tick(0.016f); };
            _tick();
            int _count = CountAllocations(_tick);
            Assert.AreEqual(0, _count, "The reverb path of the acoustic tick allocated managed memory.");
        }

        [UnityTest]
        public IEnumerator Reverb_ZeroGC_PositiveControl() {
            yield return null;
            int _count = CountAllocations(() => { object _sink = new byte[64]; GC.KeepAlive(_sink); });
            Assert.Greater(_count, 0, "The recorder did not see a known allocation.");
        }
    }
}
