using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HarryTools.Audio.Tests {
    public class AcousticContractTests {
        // ==================== FAKES ===================
        private sealed class FakeModule : IAcousticModule {
            public int OrderValue;
            public float Interval;
            public float Volume = 0.5f;
            public float LowPass = 1000f;
            public float HighPass = 10f;
            public bool SetPerceived;
            public Vector3 Perceived;
            public bool Throw;
            public int Calls;
            public readonly List<AudioHandle> LastHandles = new List<AudioHandle>();

            public int Order => OrderValue;
            public float UpdateInterval => Interval;

            public void Evaluate(ReadOnlySpan<AcousticQuery> _queries, Span<AcousticResult> _results) {
                Calls++;
                if (Throw) throw new InvalidOperationException("Module failed on purpose");
                LastHandles.Clear();
                for (int _i = 0; _i < _queries.Length; _i++) {
                    LastHandles.Add(_queries[_i].Handle);
                    AcousticResult _r = _results[_i];
                    _r.VolumeMultiplier *= Volume;
                    _r.LowPassCutoff = Mathf.Min(_r.LowPassCutoff, LowPass);
                    _r.HighPassCutoff = Mathf.Max(_r.HighPassCutoff, HighPass);
                    if (SetPerceived) {
                        _r.HasPerceivedPosition = true;
                        _r.PerceivedPosition = Perceived;
                    }
                    _results[_i] = _r;
                }
            }
        }

        // Low-pass proportional to the source x position: each voice gets its own value.
        private sealed class PositionModule : IAcousticModule {
            public int Order => 0;
            public float UpdateInterval => 5f;
            public void Evaluate(ReadOnlySpan<AcousticQuery> _queries, Span<AcousticResult> _results) {
                for (int _i = 0; _i < _queries.Length; _i++) {
                    AcousticResult _r = _results[_i];
                    _r.LowPassCutoff = 100f * _queries[_i].SourcePosition.x;
                    _results[_i] = _r;
                }
            }
        }

        private sealed class AllocatingModule : IAcousticModule {
            public int Order => 0;
            public float UpdateInterval => 0f;
            public object Sink;
            public void Evaluate(ReadOnlySpan<AcousticQuery> _queries, Span<AcousticResult> _results) => Sink = new byte[64];
        }

        // ==================== VARIABLES ===================
        private const string TestKey = "HarryTools.Audio.Tests.Acoustic";

        private readonly List<Object> created = new List<Object>();
        private readonly List<IAcousticModule> registered = new List<IAcousticModule>();
        private AudioProjectSettings settings;
        private GameObject listener;
        private SoundAsset sound;

        // ==================== SETUP ===================
        [SetUp]
        public void SetUp() {
            PlayerPrefs.DeleteKey(TestKey);
            listener = new GameObject("Test Listener", typeof(AudioListener));
            Build(10);
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
        private void Build(int _maxSimulated) {
            settings = ScriptableObject.CreateInstance<AudioProjectSettings>();
            settings.MaxVoices = 8;
            settings.PrewarmVoices = 8;
            settings.MaxSimulated = _maxSimulated;
            settings.PersistenceKey = TestKey;
            created.Add(settings);
            Audio.InitializeForTests(settings);

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

        private FakeModule Register(FakeModule _module) {
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
        public IEnumerator Module_AppliesVolumeAndLowPass_Smoothed() {
            Register(new FakeModule());
            AudioHandle _handle = Audio.Play(sound, new Vector3(2f, 0f, 0f));
            AudioVoice _voice = VoiceOf(_handle);
            Assert.AreEqual(1f, _voice.AcousticVolume, 0.0001f);
            yield return null;
            Assert.Greater(_voice.AcousticVolume, 0.5f, "The multiplier must not jump to the target.");
            yield return Seconds(0.5f);
            Assert.AreEqual(0.5f, _voice.AcousticVolume, 0.02f);
            Assert.IsTrue(_voice.LowPass.enabled);
            Assert.AreEqual(1000f, _voice.LowPass.cutoffFrequency, 30f);
            Assert.IsFalse(_voice.HighPass.enabled);
        }

        [UnityTest]
        public IEnumerator Budget_OnlyTopNSimulated() {
            Build(2);
            FakeModule _module = Register(new FakeModule());
            AudioHandle _far = Audio.Play(sound, new Vector3(40f, 0f, 0f));
            AudioHandle _near = Audio.Play(sound, new Vector3(1f, 0f, 0f));
            AudioHandle _farther = Audio.Play(sound, new Vector3(49f, 0f, 0f));
            AudioHandle _mid = Audio.Play(sound, new Vector3(5f, 0f, 0f));
            yield return null;
            yield return null;
            Assert.AreEqual(2, _module.LastHandles.Count);
            Assert.AreEqual(_near, _module.LastHandles[0]);
            Assert.AreEqual(_mid, _module.LastHandles[1]);
            yield return Seconds(0.4f);
            Assert.AreEqual(1f, VoiceOf(_far).AcousticVolume, 0.0001f);
            Assert.AreEqual(1f, VoiceOf(_farther).AcousticVolume, 0.0001f);
        }

        [UnityTest]
        public IEnumerator NoModules_PlaybackUnchanged() {
            AudioVoice _voice = VoiceOf(Audio.Play(sound, new Vector3(3f, 0f, 0f)));
            yield return Seconds(0.2f);
            Assert.AreEqual(1f, _voice.AcousticVolume, 0.0001f);
            Assert.IsFalse(_voice.LowPass.enabled);
            Assert.IsFalse(_voice.HighPass.enabled);
            Assert.AreEqual(new Vector3(3f, 0f, 0f), _voice.transform.position);
        }

        [UnityTest]
        public IEnumerator Unregister_ReturnsToNeutral() {
            FakeModule _module = Register(new FakeModule { SetPerceived = true, Perceived = new Vector3(0f, 9f, 0f) });
            AudioVoice _voice = VoiceOf(Audio.Play(sound, new Vector3(2f, 0f, 0f)));
            yield return Seconds(0.5f);
            Assert.AreEqual(0.5f, _voice.AcousticVolume, 0.02f);
            Audio.UnregisterModule(_module);
            yield return Seconds(1.2f);
            Assert.AreEqual(1f, _voice.AcousticVolume, 0.01f);
            Assert.IsFalse(_voice.LowPass.enabled, "Back to neutral must switch the low pass off.");
            Assert.AreEqual(new Vector3(2f, 0f, 0f), _voice.transform.position);
        }

        [UnityTest]
        public IEnumerator ModuleThrows_IsRemoved_AudioKeepsPlaying() {
            FakeModule _bad = Register(new FakeModule { Throw = true, OrderValue = 0 });
            Register(new FakeModule { OrderValue = 1 });
            // The failure is logged with Debug.LogException, which the test runner reports as an Exception log.
            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("Module failed on purpose"));
            AudioHandle _handle = Audio.Play(sound, new Vector3(2f, 0f, 0f));
            yield return Seconds(0.5f);
            Assert.AreEqual(1, _bad.Calls, "A throwing module must be unregistered after its first failure.");
            Assert.IsTrue(_handle.IsValid);
            Assert.IsTrue(_handle.IsPlaying);
            Assert.AreEqual(0.5f, VoiceOf(_handle).AcousticVolume, 0.02f, "The other modules keep running.");
        }

        [UnityTest]
        public IEnumerator ModuleLowPass_CombinesWithPreset_UsesLowest() {
            Register(new FakeModule { Volume = 1f, LowPass = 1000f });
            var _preset = ScriptableObject.CreateInstance<AudioEffectPreset>();
            _preset.LowPass = true;
            _preset.LowPassCutoff = 3000f;
            created.Add(_preset);
            AudioVoice _voice = VoiceOf(Audio.Play(sound, new Vector3(2f, 0f, 0f)));
            EffectApplier.Apply(_voice, _preset, null);
            yield return Seconds(1f);
            Assert.IsTrue(_voice.LowPass.enabled);
            Assert.AreEqual(1000f, _voice.LowPass.cutoffFrequency, 20f);

            _preset.LowPassCutoff = 800f;
            EffectApplier.Apply(_voice, _preset, null);
            yield return Seconds(0.3f);
            Assert.AreEqual(800f, _voice.LowPass.cutoffFrequency, 20f);
        }

        [UnityTest]
        public IEnumerator ModuleHighPass_CombinesWithPreset_UsesHighest() {
            Register(new FakeModule { Volume = 1f, LowPass = 22000f, HighPass = 300f });
            var _preset = ScriptableObject.CreateInstance<AudioEffectPreset>();
            _preset.HighPass = true;
            _preset.HighPassCutoff = 500f;
            created.Add(_preset);
            AudioVoice _voice = VoiceOf(Audio.Play(sound, new Vector3(2f, 0f, 0f)));
            EffectApplier.Apply(_voice, _preset, null);
            yield return Seconds(1f);
            Assert.IsTrue(_voice.HighPass.enabled);
            Assert.AreEqual(500f, _voice.HighPass.cutoffFrequency, 10f);
        }

        [UnityTest]
        public IEnumerator PerceivedPosition_MovesSourceNotLogical() {
            Vector3 _logical = new Vector3(2f, 0f, 0f);
            Vector3 _perceived = new Vector3(0f, 7f, 0f);
            Register(new FakeModule { SetPerceived = true, Perceived = _perceived, Volume = 1f, LowPass = 22000f });
            AudioHandle _handle = Audio.Play(sound, _logical);
            AudioVoice _voice = VoiceOf(_handle);
            yield return null;
            yield return null;
            Assert.AreEqual(_perceived, _voice.transform.position);
            Assert.AreEqual(_logical, _voice.LogicalPosition);

            Vector3 _moved = new Vector3(3f, 0f, 0f);
            _handle.At(_moved);
            yield return null;
            Assert.AreEqual(_perceived, _voice.transform.position);
            Assert.AreEqual(_moved, _voice.LogicalPosition);
        }

        [UnityTest]
        public IEnumerator TwoDVoices_NotSimulated() {
            sound.SetSpatial(default(SpatialSettings));
            FakeModule _module = Register(new FakeModule());
            AudioHandle _handle = Audio.Play(sound, Vector3.zero);
            Assert.AreEqual(0f, VoiceOf(_handle).Source.spatialBlend);
            yield return Seconds(0.3f);
            Assert.AreEqual(0, _module.Calls);
            Assert.AreEqual(1f, VoiceOf(_handle).AcousticVolume, 0.0001f);
        }

        [UnityTest]
        public IEnumerator VoiceReuse_ResetsAcousticState() {
            settings.MaxVoices = 1;
            settings.PrewarmVoices = 1;
            Audio.InitializeForTests(settings);
            Register(new FakeModule { SetPerceived = true, Perceived = new Vector3(0f, 9f, 0f) });
            AudioHandle _first = Audio.Play(sound, new Vector3(2f, 0f, 0f));
            yield return Seconds(0.4f);
            _first.Stop(0f);
            AudioHandle _second = Audio.Play(sound, new Vector3(4f, 0f, 0f));
            AudioVoice _voice = VoiceOf(_second);
            Assert.AreEqual(_first.VoiceIndex, _second.VoiceIndex);
            Assert.AreEqual(1f, _voice.AcousticVolume, 0.0001f);
            Assert.IsFalse(_voice.LowPass.enabled);
            Assert.AreEqual(new Vector3(4f, 0f, 0f), _voice.transform.position);
        }

        [UnityTest]
        public IEnumerator WithoutMixer_NothingApplied() {
            AudioPlatform.OverrideUsesMixer(false);
            Register(new FakeModule { SetPerceived = true, Perceived = new Vector3(0f, 9f, 0f) });
            AudioVoice _voice = VoiceOf(Audio.Play(sound, new Vector3(2f, 0f, 0f)));
            yield return Seconds(0.4f);
            Assert.AreEqual(1f, _voice.AcousticVolume, 0.0001f);
            Assert.IsFalse(_voice.LowPass.enabled);
            Assert.AreEqual(new Vector3(2f, 0f, 0f), _voice.transform.position);
        }

        [Test]
        public void RegisterModule_Twice_IsNoOp() {
            FakeModule _module = Register(new FakeModule());
            Audio.RegisterModule(_module);
            Assert.AreEqual(1, AcousticSimulator.RegisteredCount);
            Audio.UnregisterModule(_module);
            Assert.AreEqual(0, AcousticSimulator.RegisteredCount);
        }

        [UnityTest]
        public IEnumerator Modules_RunInOrder_TiesByRegistration() {
            var _a = Register(new FakeModule { OrderValue = 5, Volume = 1f, LowPass = 22000f });
            var _b = Register(new FakeModule { OrderValue = 1, Volume = 0.5f, LowPass = 22000f });
            var _c = Register(new FakeModule { OrderValue = 1, Volume = 1f, LowPass = 22000f });
            Audio.Play(sound, new Vector3(2f, 0f, 0f));
            yield return null;
            yield return null;
            Assert.GreaterOrEqual(_a.Calls, 1);
            Assert.GreaterOrEqual(_b.Calls, 1);
            Assert.AreSame(_b, AcousticSimulator.ModuleAt(0));
            Assert.AreSame(_c, AcousticSimulator.ModuleAt(1));
            Assert.AreSame(_a, AcousticSimulator.ModuleAt(2));
        }

        [UnityTest]
        public IEnumerator UpdateInterval_SkipsTicks_KeepsLastResult() {
            FakeModule _module = Register(new FakeModule { Interval = 10f });
            AudioVoice _voice = VoiceOf(Audio.Play(sound, new Vector3(2f, 0f, 0f)));
            yield return Seconds(0.5f);
            Assert.AreEqual(1, _module.Calls, "The module only runs when its interval is due.");
            Assert.AreEqual(0.5f, _voice.AcousticVolume, 0.02f, "The last result keeps being applied between evaluations.");
        }

        [UnityTest]
        public IEnumerator AcousticSimulator_Tick_ZeroGC() {
            Register(new FakeModule { SetPerceived = true, Perceived = new Vector3(0f, 3f, 0f) });
            for (int _i = 0; _i < 4; _i++) Audio.Play(sound, new Vector3(2f + _i, 0f, 0f));
            yield return Seconds(0.3f);
            AudioRuntime _runtime = Audio.Runtime;
            Action _tick = () => { for (int _i = 0; _i < 100; _i++) _runtime.Acoustics.Tick(0.016f); };
            _tick();
            int _count = CountAllocations(_tick);
            Assert.AreEqual(0, _count, "The acoustic simulator tick allocated managed memory.");
        }

        [UnityTest]
        public IEnumerator ChainedModules_IntervalGated_KeepsTightening() {
            FakeModule _a = Register(new FakeModule { OrderValue = 0, Volume = 1f, LowPass = 500f });
            FakeModule _b = Register(new FakeModule { OrderValue = 1, Interval = 0.2f, Volume = 1f, LowPass = 1000f });
            AudioVoice _voice = VoiceOf(Audio.Play(sound, new Vector3(2f, 0f, 0f)));
            yield return Seconds(0.8f);
            Assert.AreEqual(500f, _voice.LowPass.cutoffFrequency, 20f);

            _a.LowPass = 5000f;
            // B only runs every 0.2 s; between runs its stored 1000 must still tighten the opened A.
            yield return Seconds(0.8f);
            Assert.AreEqual(1000f, _voice.LowPass.cutoffFrequency, 30f);
            Assert.Greater(_a.Calls, _b.Calls);
        }

        [UnityTest]
        public IEnumerator BudgetChurn_ResultsFollowTheRightVoice() {
            Build(1);
            var _position = new PositionModule();
            Audio.RegisterModule(_position);
            registered.Add(_position);
            AudioHandle _x = Audio.Play(sound, new Vector3(5f, 0f, 0f));
            AudioHandle _y = Audio.Play(sound, new Vector3(20f, 0f, 0f));
            AudioVoice _vx = VoiceOf(_x);
            AudioVoice _vy = VoiceOf(_y);
            yield return Seconds(0.8f);
            Assert.AreEqual(500f, _vx.LowPass.cutoffFrequency, 20f);
            Assert.IsFalse(_vy.LowPass.enabled);

            _x.At(new Vector3(49f, 0f, 0f));
            yield return Seconds(1.5f);
            Assert.AreEqual(2000f, _vy.LowPass.cutoffFrequency, 60f, "Y enters the budget with its own result.");
            Assert.IsFalse(_vx.LowPass.enabled, "X left the budget and must be back to neutral.");
        }

        [UnityTest]
        public IEnumerator MultipleModules_Tick_ZeroGC() {
            Register(new FakeModule { OrderValue = 0, Interval = 0f, SetPerceived = true, Perceived = new Vector3(0f, 3f, 0f) });
            Register(new FakeModule { OrderValue = 1, Interval = 0.05f, LowPass = 800f });
            Register(new FakeModule { OrderValue = 2, Interval = 10f, Volume = 0.8f });
            for (int _i = 0; _i < 4; _i++) Audio.Play(sound, new Vector3(2f + _i, 0f, 0f));
            yield return Seconds(0.3f);
            AudioRuntime _runtime = Audio.Runtime;
            Action _tick = () => { for (int _i = 0; _i < 100; _i++) _runtime.Acoustics.Tick(0.016f); };
            _tick();
            int _count = CountAllocations(_tick);
            Assert.AreEqual(0, _count, "The acoustic simulator tick with several modules allocated managed memory.");
        }

        [UnityTest]
        public IEnumerator MultipleModules_Tick_PositiveControl() {
            var _alloc = new AllocatingModule();
            Audio.RegisterModule(_alloc);
            registered.Add(_alloc);
            Register(new FakeModule { OrderValue = 1, Interval = 10f });
            Register(new FakeModule { OrderValue = 2, Interval = 0.05f });
            Audio.Play(sound, new Vector3(2f, 0f, 0f));
            yield return Seconds(0.1f);
            AudioRuntime _runtime = Audio.Runtime;
            Action _tick = () => _runtime.Acoustics.Tick(0.016f);
            Assert.Greater(CountAllocations(_tick), 0);
        }

        [UnityTest]
        public IEnumerator AcousticSimulator_Tick_PositiveControl() {
            // A module that allocates in Evaluate must be seen by the recorder through the simulator tick.
            var _alloc = new AllocatingModule();
            Audio.RegisterModule(_alloc);
            registered.Add(_alloc);
            Audio.Play(sound, new Vector3(2f, 0f, 0f));
            yield return Seconds(0.1f);
            AudioRuntime _runtime = Audio.Runtime;
            Action _tick = () => _runtime.Acoustics.Tick(0.016f);
            int _count = CountAllocations(_tick);
            Assert.Greater(_count, 0);
        }
    }
}
