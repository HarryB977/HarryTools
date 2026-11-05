using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HarryTools.Audio.Tests {
    public class AllocationTests {
        // ==================== VARIABLES ===================
        private const string TestKey = "HarryTools.Audio.Tests.Allocation";
        private const int Plays = 100;

        private readonly List<Object> created = new List<Object>();
        private AudioProjectSettings settings;
        private GameObject listener;
        private SoundAsset sound;

        // ==================== SETUP ===================
        [SetUp]
        public void SetUp() {
            listener = new GameObject("Test Listener", typeof(AudioListener));
            settings = ScriptableObject.CreateInstance<AudioProjectSettings>();
            settings.MaxVoices = 8;
            settings.PrewarmVoices = 8;
            settings.PersistenceKey = TestKey;
            created.Add(settings);
            Audio.InitializeForTests(settings);

            int _samples = 44100;
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
            Audio.ShutdownForTests();
            PlayerPrefs.DeleteKey(TestKey);
            if (listener != null) Object.DestroyImmediate(listener);
            for (int _i = 0; _i < created.Count; _i++) {
                if (created[_i] != null) Object.Destroy(created[_i]);
            }
            created.Clear();
        }

        // ==================== HELPERS ===================
        private static int CountAllocations(Action _action) {
            Recorder _recorder = Recorder.Get("GC.Alloc");
            _recorder.FilterToCurrentThread();
            _recorder.enabled = true;
            _action();
            _recorder.enabled = false;
            return _recorder.sampleBlockCount;
        }

        private void PlayAndStop() {
            Vector3 _position = new Vector3(1f, 0f, 2f);
            for (int _i = 0; _i < Plays; _i++) {
                AudioHandle _handle = Audio.Play(sound, _position);
                _handle.Stop(0f);
            }
        }

        private void PlayStealing() {
            Vector3 _position = new Vector3(1f, 0f, 2f);
            for (int _i = 0; _i < Plays; _i++) Audio.Play(sound, _position);
        }

        // ==================== TESTS ===================
        [Test]
        public void Recorder_DetectsAllocations_PositiveControl() {
            object _sink = null;
            Action _allocate = () => { for (int _i = 0; _i < 10; _i++) _sink = new byte[64]; };
            int _count = CountAllocations(_allocate);
            Assert.IsNotNull(_sink);
            Assert.Greater(_count, 0, "The GC.Alloc recorder must see managed allocations.");
        }

        [Test]
        public void Play_FirstPlayOfAsset_AllocatesItsRuntimeState_PositiveControl() {
            // The first play of an asset creates its ClipSelector and state entry: the recorder must see it through Audio.Play.
            Vector3 _position = Vector3.zero;
            AudioHandle _handle = AudioHandle.Invalid;
            Action _first = () => _handle = Audio.Play(sound, _position);
            int _count = CountAllocations(_first);
            Assert.IsTrue(_handle.IsValid);
            Assert.Greater(_count, 0);
        }

        [UnityTest]
        public IEnumerator Play_SteadyState_ZeroGC() {
            Action _run = PlayAndStop;
            for (int _i = 0; _i < 10; _i++) _run();
            yield return null;
            Assert.IsTrue(Audio.Play(sound, Vector3.zero).IsValid, "Steady-state plays must actually start.");
            Audio.StopAll();
            int _count = CountAllocations(_run);
            Assert.AreEqual(0, _count, "Audio.Play + Stop allocated managed memory in steady state.");
        }

        [UnityTest]
        public IEnumerator Play_StealingInstances_SteadyState_ZeroGC() {
            sound.SetLimits(0f, 2, InstanceLimitPolicy.StealOldest);
            Action _run = PlayStealing;
            for (int _i = 0; _i < 10; _i++) _run();
            yield return null;
            Assert.IsTrue(Audio.Play(sound, Vector3.zero).IsValid, "Stealing plays must actually start.");
            int _count = CountAllocations(_run);
            Assert.AreEqual(0, _count, "Audio.Play with instance stealing allocated managed memory in steady state.");
        }

        [UnityTest]
        public IEnumerator Play_PoolFull_SteadyState_ZeroGC() {
            Action _run = PlayStealing;
            for (int _i = 0; _i < 10; _i++) _run();
            yield return null;
            Assert.AreEqual(settings.MaxVoices, Audio.Runtime.Voices.ActiveVoices.Count);
            int _count = CountAllocations(_run);
            Assert.AreEqual(0, _count, "Audio.Play against a full pool allocated managed memory.");
        }

        [UnityTest]
        public IEnumerator Tick_SteadyState_ZeroGC() {
            Vector3 _position = new Vector3(1f, 0f, 2f);
            for (int _i = 0; _i < 4; _i++) Audio.Play(sound, _position);
            yield return null;
            AudioRuntime _runtime = Audio.Runtime;
            Action _tick = () => { for (int _i = 0; _i < Plays; _i++) _runtime.Voices.Tick(0.001f); };
            _tick();
            int _count = CountAllocations(_tick);
            Assert.AreEqual(0, _count, "The voice tick allocated managed memory.");
        }

        [UnityTest]
        public IEnumerator Play_WithoutPosition_SteadyState_ZeroGC() {
            Action _run = () => { for (int _i = 0; _i < Plays; _i++) Audio.Play(sound).Stop(0f); };
            for (int _i = 0; _i < 10; _i++) _run();
            yield return null;
            int _count = CountAllocations(_run);
            Assert.AreEqual(0, _count, "Audio.Play following the listener allocated managed memory.");
        }

        [UnityTest]
        public IEnumerator NestedStopAll_FromOnEnded_ZeroGC() {
            var _other = ScriptableObject.CreateInstance<SoundAsset>();
            _other.SetClips(new[] { new ClipEntry(sound.Clips[0].Clip) });
            created.Add(_other);
            Action<AudioHandle> _nested = _ => Audio.Stop(_other, 0f);
            Vector3 _position = Vector3.one;
            Action _run = () => {
                for (int _i = 0; _i < Plays; _i++) {
                    Audio.Play(_other, _position);
                    Audio.Play(sound, _position).OnEnded(_nested);
                    Audio.StopAll();
                }
            };
            for (int _i = 0; _i < 10; _i++) _run();
            yield return null;
            int _count = CountAllocations(_run);
            Assert.AreEqual(0, _count, "A StopAll nested in an OnEnded callback allocated managed memory.");
        }

        [UnityTest]
        public IEnumerator EmitterQueue_SteadyState_ZeroGC() {
            var _speaker = ScriptableObject.CreateInstance<SpeakerAsset>();
            var _line = ScriptableObject.CreateInstance<VoiceAsset>();
            _line.SetClips(new[] { new ClipEntry(sound.Clips[0].Clip) });
            _line.SetSpeaker(_speaker);
            var _effect = ScriptableObject.CreateInstance<AudioEffectPreset>();
            _effect.LowPass = true;
            created.Add(_speaker);
            created.Add(_line);
            created.Add(_effect);
            var _go = new GameObject("Emitter");
            created.Add(_go);
            var _emitter = _go.AddComponent<AudioEmitter>();
            _emitter.Speaker = _speaker;
            _emitter.Effect = _effect;
            int _started = 0;
            Action<VoiceLineInfo> _onStarted = _ => _started++;
            Audio.Voices.LineStarted += _onStarted;
            VoiceChannel _channel = Audio.Voices;

            // Say two lines (the second is queued), stop the first so the queued one starts, then stop it.
            Action _run = () => {
                for (int _i = 0; _i < Plays; _i++) {
                    AudioHandle _first = _emitter.Say(_line);
                    AudioHandle _second = _emitter.Say(_line);
                    _first.Stop(0f);
                    _channel.Tick();
                    _second.Stop(0f);
                }
            };
            for (int _i = 0; _i < 10; _i++) _run();
            yield return null;
            _started = 0;
            int _count = CountAllocations(_run);
            Audio.Voices.LineStarted -= _onStarted;
            Assert.AreEqual(2 * Plays, _started, "Both lines must start on every iteration.");
            Assert.AreEqual(0, _count, "Saying, queueing and starting queued lines allocated managed memory.");
        }
    }
}
