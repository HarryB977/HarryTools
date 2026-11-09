using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HarryTools.Audio.Tests {
    public class ReverbTailTests {
        // ==================== FAKES ===================
        private sealed class ReverbModule : IAcousticModule {
            public float Wet = 0.6f;
            public float Decay = 3f;

            public int Order => 0;
            public float UpdateInterval => 0f;

            public void Evaluate(ReadOnlySpan<AcousticQuery> _queries, Span<AcousticResult> _results) {
                for (int _i = 0; _i < _queries.Length; _i++) {
                    AcousticResult _r = _results[_i];
                    _r.HasReverb = true;
                    _r.Reverb = new AcousticReverb { Wet = Wet, DecayTime = Decay, HighFrequencyRatio = 0.7f, ReflectionsDelay = 0.02f };
                    _results[_i] = _r;
                }
            }
        }

        // Runs on the audio thread, after the reverb filter of the voice.
        private sealed class RmsMeter : MonoBehaviour {
            private double rms;
            public double Rms => Volatile.Read(ref rms);

            private void OnAudioFilterRead(float[] _data, int _channels) {
                double _sum = 0d;
                for (int _i = 0; _i < _data.Length; _i++) _sum += _data[_i] * _data[_i];
                Volatile.Write(ref rms, Math.Sqrt(_sum / Math.Max(1, _data.Length)));
            }
        }

        // ==================== VARIABLES ===================
        private const string TestKey = "HarryTools.Audio.Tests.ReverbTail";

        private readonly List<Object> created = new List<Object>();
        private readonly List<IAcousticModule> registered = new List<IAcousticModule>();
        private GameObject listener;
        private int ended;
        private Action<AudioHandle> onEnded;

        // ==================== SETUP ===================
        [SetUp]
        public void SetUp() {
            PlayerPrefs.DeleteKey(TestKey);
            listener = new GameObject("Test Listener", typeof(AudioListener));
            var _settings = ScriptableObject.CreateInstance<AudioProjectSettings>();
            _settings.MaxVoices = 4;
            _settings.PrewarmVoices = 4;
            _settings.MaxSimulated = 4;
            _settings.PersistenceKey = TestKey;
            created.Add(_settings);
            Audio.InitializeForTests(_settings);
            ended = 0;
            onEnded = _ => ended++;
            Audio.Ended += onEnded;
        }

        [TearDown]
        public void TearDown() {
            Audio.Ended -= onEnded;
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
        private static VoiceManager Manager => Audio.Runtime.Voices;

        private ReverbModule Register(float _wet = 0.6f, float _decay = 3f) {
            var _module = new ReverbModule { Wet = _wet, Decay = _decay };
            Audio.RegisterModule(_module);
            registered.Add(_module);
            return _module;
        }

        private SoundAsset MakeSound(float _seconds, float _amplitude = 0.2f, int _priority = 50) {
            int _samples = Mathf.CeilToInt(44100 * _seconds);
            var _clip = AudioClip.Create("Sine", _samples, 1, 44100, false);
            var _data = new float[_samples];
            for (int _i = 0; _i < _samples; _i++) _data[_i] = _amplitude * Mathf.Sin(2f * Mathf.PI * 440f * _i / 44100f);
            _clip.SetData(_data, 0);
            created.Add(_clip);
            var _asset = ScriptableObject.CreateInstance<SoundAsset>();
            _asset.SetClips(new[] { new ClipEntry(_clip) });
            _asset.SetSpatial(SpatialSettings.Default3D);
            _asset.SetPriority(_priority);
            created.Add(_asset);
            return _asset;
        }

        private static AudioHandle PlayAt(SoundAsset _sound) => Audio.Play(_sound, new Vector3(2f, 0f, 0f));

        private static AudioVoice VoiceOf(AudioHandle _handle) {
            Assert.IsTrue(Manager.TryGetVoice(_handle, out AudioVoice _voice), "Handle has no voice.");
            return _voice;
        }

        private static IEnumerator Seconds(float _seconds) {
            float _end = Time.realtimeSinceStartup + _seconds;
            while (Time.realtimeSinceStartup < _end) yield return null;
        }

        private static IEnumerator WaitFor(Func<bool> _condition, float _timeout) {
            float _end = Time.realtimeSinceStartup + _timeout;
            while (!_condition() && Time.realtimeSinceStartup < _end) yield return null;
        }

        private IEnumerator PlayIntoTail(SoundAsset _sound) {
            int _before = Manager.TailCount;
            PlayAt(_sound);
            yield return WaitFor(() => Manager.TailCount > _before, 3f);
            Assert.AreEqual(_before + 1, Manager.TailCount, "The voice did not enter its tail.");
        }

        // ==================== TESTS ===================
        [UnityTest]
        public IEnumerator Tail_KeepsEnergyAfterClipEnds() {
            Register(1f, 3f);
            AudioVoice _voice = VoiceOf(PlayAt(MakeSound(0.4f)));
            RmsMeter _meter = _voice.gameObject.AddComponent<RmsMeter>();
            yield return WaitFor(() => Manager.TailCount == 1, 3f);
            Assert.AreEqual(1, Manager.TailCount);

            yield return Seconds(0.05f);
            double _peak = 0d;
            float _end = Time.realtimeSinceStartup + 0.3f;
            while (Time.realtimeSinceStartup < _end) {
                _peak = Math.Max(_peak, _meter.Rms);
                yield return null;
            }
            Assert.Greater(_peak, 0.001d, "No reverb energy after the clip ended.");
            Object.Destroy(_meter);
        }

        [UnityTest]
        public IEnumerator NoReverb_NoTail() {
            AudioHandle _handle = PlayAt(MakeSound(0.1f));
            AudioVoice _voice = VoiceOf(_handle);
            yield return WaitFor(() => ended > 0, 2f);
            Assert.AreEqual(1, ended);
            Assert.AreEqual(0, Manager.TailCount);
            Assert.IsFalse(_voice.IsActive);
            Assert.IsFalse(_voice.IsTail);
            Assert.IsNull(_voice.Source.clip);
        }

        [UnityTest]
        public IEnumerator LowWet_NoTail() {
            Register(0.005f);
            PlayAt(MakeSound(0.3f));
            yield return WaitFor(() => ended > 0, 2f);
            Assert.AreEqual(1, ended);
            Assert.AreEqual(0, Manager.TailCount);
        }

        [UnityTest]
        public IEnumerator WebGL_NoTail() {
            AudioPlatform.OverrideUsesMixer(false);
            Register(1f);
            PlayAt(MakeSound(0.3f));
            yield return WaitFor(() => ended > 0, 2f);
            Assert.AreEqual(1, ended);
            Assert.AreEqual(0, Manager.TailCount);
        }

        [UnityTest]
        public IEnumerator Ended_RaisedAtClipEnd_NotAtTailEnd() {
            Register(0.8f, 3f);
            int _callback = 0;
            AudioHandle _handle = PlayAt(MakeSound(0.3f)).OnEnded(_ => _callback++);
            float _start = Time.realtimeSinceStartup;
            yield return WaitFor(() => ended > 0, 2f);
            float _elapsed = Time.realtimeSinceStartup - _start;

            Assert.AreEqual(1, ended);
            Assert.AreEqual(1, _callback);
            Assert.Less(_elapsed, 0.8f, "Ended waited for the tail.");
            Assert.IsFalse(_handle.IsValid);
            Assert.AreEqual(1, Manager.TailCount);
            Assert.AreEqual(0, Manager.ActiveVoices.Count);
        }

        [UnityTest]
        public IEnumerator Tail_ReleasedAfterDecay() {
            Register(0.8f, 0.5f);
            AudioVoice _voice = VoiceOf(PlayAt(MakeSound(0.3f)));
            yield return WaitFor(() => Manager.TailCount == 1, 2f);
            Assert.AreEqual(1, Manager.TailCount);
            float _start = Time.realtimeSinceStartup;
            Assert.IsTrue(_voice.IsTail);

            yield return Seconds(0.25f);
            Assert.AreEqual(1, Manager.TailCount, "Released before the decay time.");
            yield return WaitFor(() => Manager.TailCount == 0, 2f);
            Assert.Less(Time.realtimeSinceStartup - _start, 0.8f);
            Assert.IsFalse(_voice.IsTail);
        }

        [UnityTest]
        public IEnumerator Tail_CappedAtFiveSeconds() {
            Register(0.8f, 20f);
            yield return PlayIntoTail(MakeSound(0.3f));
            for (int _i = 0; _i < 4; _i++) Manager.Tick(1f);
            Assert.AreEqual(1, Manager.TailCount, "Released before the five second cap.");
            Manager.Tick(1.1f);
            Assert.AreEqual(0, Manager.TailCount, "The tail outlived the five second cap.");
        }

        [UnityTest]
        public IEnumerator Stop_WithReverb_StartsTail() {
            Register(0.8f, 3f);
            AudioHandle _handle = PlayAt(MakeSound(2f));
            yield return Seconds(0.5f);
            Assert.AreEqual(0, ended);

            _handle.Stop(0.1f);
            Assert.AreEqual(0, Manager.TailCount, "The tail starts when the fade ends, not before.");
            Assert.AreEqual(0, ended);
            yield return WaitFor(() => ended > 0, 1f);

            Assert.AreEqual(1, ended);
            Assert.IsFalse(_handle.IsValid);
            Assert.AreEqual(1, Manager.TailCount);
        }

        [UnityTest]
        public IEnumerator SilentClip_IsSharedByTails() {
            Register(0.8f, 5f);
            SoundAsset _sound = MakeSound(0.3f);
            AudioVoice _first = VoiceOf(PlayAt(_sound));
            AudioVoice _second = VoiceOf(PlayAt(_sound));
            yield return WaitFor(() => Manager.TailCount == 2, 3f);
            Assert.AreEqual(2, Manager.TailCount);
            Assert.AreSame(_first.Source.clip, _second.Source.clip);
            Assert.AreEqual(VoiceManager.TailClipName, _first.Source.clip.name);
            Assert.IsTrue(_first.Source.loop);
        }

        [UnityTest]
        public IEnumerator ExplicitStopZero_NoTail() {
            Register(0.8f, 3f);
            AudioHandle _handle = PlayAt(MakeSound(2f));
            AudioVoice _voice = VoiceOf(_handle);
            yield return Seconds(0.5f);
            _handle.Stop(0f);
            Assert.AreEqual(1, ended);
            Assert.AreEqual(0, Manager.TailCount);
            Assert.IsFalse(_voice.IsTail);
            Assert.IsFalse(_voice.Reverb.enabled);
        }

        [UnityTest]
        public IEnumerator DefaultStop_NoFadeOut_StartsTail() {
            Register(0.8f, 3f);
            SoundAsset _sound = MakeSound(2f);
            Assert.AreEqual(0f, _sound.FadeOut, "The test needs an asset without FadeOut.");
            AudioHandle _handle = PlayAt(_sound);
            AudioVoice _voice = VoiceOf(_handle);
            yield return Seconds(0.5f);

            _handle.Stop();
            Assert.AreEqual(1, ended, "Ended belongs to the Stop, not to the end of the tail.");
            Assert.IsFalse(_handle.IsValid);
            Assert.AreEqual(1, Manager.TailCount);
            Assert.IsTrue(_voice.IsTail);
            Assert.IsTrue(_voice.Reverb.enabled);
            Assert.AreEqual(0, Manager.ActiveVoices.Count);
        }

        [UnityTest]
        public IEnumerator DefaultStop_WhilePaused_NoTail() {
            Register(0.8f, 3f);
            AudioHandle _handle = PlayAt(MakeSound(2f));
            yield return Seconds(0.5f);
            _handle.Pause();
            _handle.Stop();
            Assert.AreEqual(1, ended);
            Assert.AreEqual(0, Manager.TailCount);
        }

        [UnityTest]
        public IEnumerator StolenVoice_HasNoTail() {
            Register(0.8f, 3f);
            SoundAsset _sound = MakeSound(2f);
            _sound.SetLimits(0f, 1, InstanceLimitPolicy.StealOldest);
            PlayAt(_sound);
            yield return Seconds(0.5f);
            PlayAt(_sound);
            Assert.AreEqual(1, ended);
            Assert.AreEqual(0, Manager.TailCount);
        }

        [UnityTest]
        public IEnumerator Tail_StolenFirst_WhenPoolFull() {
            Register(0.8f, 5f);
            SoundAsset _short = MakeSound(0.3f);
            for (int _i = 0; _i < 3; _i++) yield return PlayIntoTail(_short);

            // The last free voice goes to a high priority sound that must survive.
            AudioHandle _live = PlayAt(MakeSound(4f, 0.2f, 100));
            yield return null;
            Assert.AreEqual(3, Manager.TailCount);
            Assert.AreEqual(1, Manager.ActiveVoices.Count);

            AudioHandle _new = PlayAt(MakeSound(4f, 0.001f, 0));
            Assert.IsTrue(_new.IsValid, "A tail must never make a new Play fail.");
            Assert.IsTrue(_live.IsValid, "A tail must be stolen before a live voice.");
            Assert.AreEqual(2, Manager.TailCount);
            Assert.AreEqual(2, Manager.ActiveVoices.Count);
        }

        [UnityTest]
        public IEnumerator StaleHandle_DuringTail_DoesNothing() {
            Register(0.8f, 3f);
            AudioHandle _handle = PlayAt(MakeSound(0.3f));
            AudioVoice _voice = VoiceOf(_handle);
            yield return WaitFor(() => Manager.TailCount == 1, 2f);
            Assert.AreEqual(1, Manager.TailCount);

            float _volume = _voice.Source.volume;
            Assert.IsFalse(_handle.IsValid);
            Assert.IsFalse(_handle.IsPlaying);
            Assert.IsFalse(Manager.TryGetVoice(_handle, out _));
            _handle.SetVolume(0.1f);
            _handle.Pause();
            _handle.Stop(0f);

            Assert.AreEqual(1, Manager.TailCount);
            Assert.IsTrue(_voice.IsTail);
            Assert.IsFalse(_voice.IsPaused);
            Assert.AreEqual(_volume, _voice.Source.volume);
            Assert.IsTrue(_voice.Source.isPlaying);
        }

        [UnityTest]
        public IEnumerator StopAll_CutsTails() {
            Register(0.8f, 5f);
            yield return PlayIntoTail(MakeSound(0.3f));
            AudioHandle _fading = PlayAt(MakeSound(2f));
            yield return Seconds(0.4f);
            int _endedBefore = ended;

            Audio.StopAll(0.1f);
            Assert.AreEqual(0, Manager.TailCount);
            yield return WaitFor(() => !_fading.IsValid, 1f);
            yield return null;
            Assert.AreEqual(_endedBefore + 1, ended);
            Assert.AreEqual(0, Manager.TailCount, "A voice fading out under StopAll must not start a tail.");
        }

        [UnityTest]
        public IEnumerator StopByAsset_KeepsTails() {
            Register(0.8f, 5f);
            SoundAsset _sound = MakeSound(0.3f);
            yield return PlayIntoTail(_sound);
            Audio.Stop(_sound, 0f);
            Assert.AreEqual(1, Manager.TailCount);
        }

        [UnityTest]
        public IEnumerator Pause_FreezesTail() {
            Register(0.8f, 0.5f);
            AudioVoice _voice = VoiceOf(PlayAt(MakeSound(0.3f)));
            yield return WaitFor(() => Manager.TailCount == 1, 2f);
            Assert.AreEqual(1, Manager.TailCount);

            Audio.Pause();
            yield return Seconds(0.9f);
            Assert.AreEqual(1, Manager.TailCount, "A paused tail must not advance.");
            Assert.IsFalse(_voice.Source.isPlaying, "A paused tail must pause its source.");

            Audio.Resume();
            Assert.IsTrue(_voice.Source.isPlaying);
            yield return WaitFor(() => Manager.TailCount == 0, 1.5f);
            Assert.AreEqual(0, Manager.TailCount);
        }

        [UnityTest]
        public IEnumerator Tail_NotCountedForInstanceLimit() {
            Register(0.8f, 5f);
            SoundAsset _sound = MakeSound(0.3f);
            _sound.SetLimits(0f, 1, InstanceLimitPolicy.Ignore);
            yield return PlayIntoTail(_sound);

            AudioHandle _again = PlayAt(_sound);
            Assert.IsTrue(_again.IsValid, "A tail must not count for the instance limit.");
        }

        [UnityTest]
        public IEnumerator Tail_NotSimulated_KeepsItsReverb() {
            ReverbModule _module = Register(0.8f, 3f);
            AudioVoice _voice = VoiceOf(PlayAt(MakeSound(0.3f)));
            yield return WaitFor(() => Manager.TailCount == 1, 2f);
            Assert.AreEqual(1, Manager.TailCount);
            float _level = _voice.Reverb.reverbLevel;
            float _wet = _voice.Acoustic.Wet;

            _module.Wet = 0.1f;
            _module.Decay = 0.2f;
            yield return Seconds(0.4f);
            Assert.AreEqual(1, Manager.TailCount);
            Assert.IsTrue(_voice.Reverb.enabled);
            Assert.AreEqual(_level, _voice.Reverb.reverbLevel, 0.001f);
            Assert.AreEqual(_wet, _voice.Acoustic.Wet, 0.0001f);
        }

        [UnityTest]
        public IEnumerator Tail_ReleasedVoiceIsCleanForNextPlay() {
            Register(0.8f, 0.3f);
            AudioVoice _voice = VoiceOf(PlayAt(MakeSound(0.3f)));
            yield return WaitFor(() => Manager.TailCount == 1, 2f);
            Assert.AreEqual(1, Manager.TailCount);
            yield return WaitFor(() => Manager.TailCount == 0, 2f);

            Assert.IsFalse(_voice.IsTail);
            Assert.IsFalse(_voice.IsActive);
            Assert.IsNull(_voice.Source.clip, "The silent clip was left on the source.");
            Assert.IsFalse(_voice.Source.loop);
            Assert.IsFalse(_voice.Source.isPlaying);
            Assert.IsFalse(_voice.Reverb.enabled);
            Assert.AreEqual(0f, _voice.Acoustic.Wet, 0.0001f);
        }

        [UnityTest]
        public IEnumerator Shutdown_WithTail_LeavesNoRuntimeBehind() {
            Register(0.8f, 5f);
            yield return PlayIntoTail(MakeSound(0.3f));
            Audio.ShutdownForTests();
            yield return null;
            Assert.IsNull(Audio.Current);
            foreach (AudioClip _clip in Resources.FindObjectsOfTypeAll<AudioClip>()) Assert.AreNotEqual(VoiceManager.TailClipName, _clip.name, "The silent clip outlived its runtime.");
        }
    }
}
