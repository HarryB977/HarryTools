using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HarryTools.Audio.Tests {
    public class PauseDuckingTests {
        // ==================== VARIABLES ===================
        private const string TestKey = "HarryTools.Audio.Tests.PauseDucking";

        private readonly List<Object> created = new List<Object>();
        private AudioProjectSettings settings;
        private GameObject listener;
        private float savedTimeScale;

        // ==================== SETUP ===================
        [SetUp]
        public void SetUp() {
            savedTimeScale = Time.timeScale;
            PlayerPrefs.DeleteKey(TestKey);
            listener = new GameObject("Test Listener", typeof(AudioListener));
            settings = ScriptableObject.CreateInstance<AudioProjectSettings>();
            settings.MaxVoices = 8;
            settings.PrewarmVoices = 8;
            settings.PersistenceKey = TestKey;
            settings.SetDucking(new List<DuckingRule>());
            created.Add(settings);
            Audio.InitializeForTests(settings);
        }

        [TearDown]
        public void TearDown() {
            Time.timeScale = savedTimeScale;
            AudioListener.pause = false;
            Audio.Resume();
            for (int _i = 0; _i < 6; _i++) Audio.Resume((AudioBus)_i);
            Audio.ShutdownForTests();
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

        private SoundAsset MakeSound(float _seconds, AudioBus _bus = AudioBus.SFX, bool _ignorePause = false, bool _followTimeScale = false) {
            var _asset = ScriptableObject.CreateInstance<SoundAsset>();
            _asset.SetClips(new[] { new ClipEntry(MakeClip(_seconds)) });
            _asset.SetBus(_bus);
            _asset.SetPauseOptions(_ignorePause, _followTimeScale);
            created.Add(_asset);
            return _asset;
        }

        private void Rebuild(params DuckingRule[] _rules) {
            settings.SetDucking(new List<DuckingRule>(_rules));
            Audio.InitializeForTests(settings);
        }

        private static AudioVoice VoiceOf(AudioHandle _handle) {
            Assert.IsTrue(Audio.Runtime.Voices.TryGetVoice(_handle, out AudioVoice _voice), "Handle has no voice.");
            return _voice;
        }

        private static float MusicOffsetDb => Audio.Runtime.Mixer.GetDuckOffsetDb(AudioBus.Music);

        private static IEnumerator WaitFor(Func<bool> _condition, float _timeout) {
            float _end = Time.realtimeSinceStartup + _timeout;
            while (!_condition() && Time.realtimeSinceStartup < _end) yield return null;
        }

        private static IEnumerator WaitReal(float _seconds) {
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

        // ==================== PAUSE ===================
        [UnityTest]
        public IEnumerator Pause_PausesNormal_NotIgnorePause() {
            AudioHandle _normal = Audio.Play(MakeSound(2f));
            AudioHandle _ignore = Audio.Play(MakeSound(2f, AudioBus.SFX, _ignorePause: true));
            yield return null;
            Audio.Pause();
            yield return null;

            Assert.IsTrue(Audio.IsPaused);
            Assert.IsTrue(_normal.IsPaused);
            Assert.IsFalse(VoiceOf(_normal).Source.isPlaying);
            Assert.IsFalse(_ignore.IsPaused);
            Assert.IsTrue(_ignore.IsPlaying);
            Assert.IsTrue(VoiceOf(_ignore).Source.ignoreListenerPause);
            Assert.IsFalse(VoiceOf(_normal).Source.ignoreListenerPause);

            Audio.Resume();
            yield return null;
            Assert.IsFalse(Audio.IsPaused);
            Assert.IsTrue(_normal.IsPlaying);
        }

        [UnityTest]
        public IEnumerator Pause_DoesNotTouchAudioListenerPause() {
            Audio.Pause();
            yield return null;
            Assert.IsFalse(AudioListener.pause);
        }

        [UnityTest]
        public IEnumerator Pause_SoundStartedWhilePaused_StartsPaused() {
            Audio.Pause();
            AudioHandle _handle = Audio.Play(MakeSound(2f));
            yield return null;
            Assert.IsTrue(_handle.IsPaused);
            Assert.IsFalse(VoiceOf(_handle).Source.isPlaying);
            Audio.Resume();
            yield return null;
            Assert.IsTrue(_handle.IsPlaying);
        }

        [UnityTest]
        public IEnumerator Resume_ContinuesFromSamePosition() {
            AudioHandle _handle = Audio.Play(MakeSound(3f));
            AudioSource _source = VoiceOf(_handle).Source;
            yield return WaitFor(() => _source.timeSamples > 2000, 1f);
            Audio.Pause();
            int _atPause = _source.timeSamples;
            Assert.Greater(_atPause, 0);

            yield return WaitReal(0.2f);
            Assert.AreEqual(_atPause, _source.timeSamples, "The position moved while paused.");

            Audio.Resume();
            yield return WaitFor(() => _source.timeSamples > _atPause, 1f);
            Assert.GreaterOrEqual(_source.timeSamples, _atPause, "Resume restarted the clip.");
            Assert.Less(_source.timeSamples, _atPause + 44100, "Resume jumped ahead.");
        }

        [UnityTest]
        public IEnumerator BusPause_OnlyAffectsBus() {
            AudioHandle _music = Audio.Play(MakeSound(2f, AudioBus.Music));
            AudioHandle _sfx = Audio.Play(MakeSound(2f, AudioBus.SFX));
            yield return null;

            Audio.Pause(AudioBus.Music);
            yield return null;
            Assert.IsTrue(Audio.IsBusPaused(AudioBus.Music));
            Assert.IsFalse(Audio.IsBusPaused(AudioBus.SFX));
            Assert.IsFalse(Audio.IsPaused);
            Assert.IsTrue(_music.IsPaused);
            Assert.IsFalse(_sfx.IsPaused);
            Assert.IsTrue(_sfx.IsPlaying);

            Audio.Resume(AudioBus.Music);
            yield return null;
            Assert.IsFalse(_music.IsPaused);
            Assert.IsTrue(_music.IsPlaying);
        }

        [UnityTest]
        public IEnumerator BusPause_CombinesWithGlobal() {
            AudioHandle _music = Audio.Play(MakeSound(2f, AudioBus.Music));
            yield return null;
            Audio.Pause();
            Audio.Pause(AudioBus.Music);
            Audio.Resume();
            yield return null;
            Assert.IsTrue(_music.IsPaused, "Still paused through its bus.");
            Audio.Resume(AudioBus.Music);
            yield return null;
            Assert.IsFalse(_music.IsPaused);
        }

        [UnityTest]
        public IEnumerator PausedVoice_DoesNotEnd() {
            AudioHandle _handle = Audio.Play(MakeSound(0.1f));
            Audio.Pause();
            yield return WaitReal(0.3f);
            Assert.IsTrue(_handle.IsValid, "A paused voice ended.");

            Audio.Resume();
            yield return WaitFor(() => !_handle.IsValid, 2f);
            Assert.IsFalse(_handle.IsValid, "The voice should end after resuming.");
        }

        [UnityTest]
        public IEnumerator HandlePause_SurvivesGlobalResume() {
            AudioHandle _handle = Audio.Play(MakeSound(3f));
            yield return null;
            _handle.Pause();
            Audio.Pause();
            Audio.Resume();
            yield return null;
            Assert.IsTrue(_handle.IsPaused);
            Assert.IsFalse(VoiceOf(_handle).Source.isPlaying);
            _handle.Resume();
            yield return null;
            Assert.IsTrue(_handle.IsPlaying);
        }

        [UnityTest]
        public IEnumerator Stop_WithFade_OnTimePausedVoice_ReleasesAtOnce() {
            SoundAsset _sound = MakeSound(3f, _followTimeScale: true);
            _sound.SetFades(0f, 1f);
            AudioHandle _handle = Audio.Play(_sound);
            yield return null;
            Time.timeScale = 0f;
            yield return null;
            _handle.Stop(0.5f);
            Assert.IsFalse(_handle.IsValid);
        }

        [Test]
        public void BusPause_InvalidBus_IsIgnored() {
            var _bad = (AudioBus)99;
            Assert.DoesNotThrow(() => Audio.Pause(_bad));
            Assert.DoesNotThrow(() => Audio.Resume(_bad));
            Assert.IsFalse(Audio.IsBusPaused(_bad));
        }

        [UnityTest]
        public IEnumerator PausedByBus_DoesNotEnd() {
            AudioHandle _handle = Audio.Play(MakeSound(0.1f, AudioBus.Music));
            Audio.Pause(AudioBus.Music);
            yield return WaitReal(0.3f);
            Assert.IsTrue(_handle.IsValid);
        }

        [UnityTest]
        public IEnumerator PausedByAudioListener_DoesNotEnd() {
            AudioHandle _handle = Audio.Play(MakeSound(0.1f));
            AudioListener.pause = true;
            yield return WaitReal(0.3f);
            Assert.IsTrue(_handle.IsValid, "A voice paused by AudioListener.pause ended.");
            AudioListener.pause = false;
        }

        [UnityTest]
        public IEnumerator Stop_OnPausedVoice_ReleasesAtOnce() {
            SoundAsset _sound = MakeSound(2f);
            _sound.SetFades(0f, 1f);
            AudioHandle _handle = Audio.Play(_sound);
            Audio.Pause();
            yield return null;
            _handle.Stop();
            Assert.IsFalse(_handle.IsValid);
        }

        // ==================== TIME SCALE ===================
        [UnityTest]
        public IEnumerator FollowTimeScale_ScalesPitch() {
            Time.timeScale = 0.5f;
            AudioHandle _follow = Audio.Play(MakeSound(2f, _followTimeScale: true));
            AudioHandle _plain = Audio.Play(MakeSound(2f));
            yield return null;
            Assert.AreEqual(0.5f, VoiceOf(_follow).Source.pitch, 0.01f);
            Assert.AreEqual(1f, VoiceOf(_plain).Source.pitch, 0.01f);

            Time.timeScale = 2f;
            yield return null;
            Assert.AreEqual(2f, VoiceOf(_follow).Source.pitch, 0.01f);
        }

        [UnityTest]
        public IEnumerator FollowTimeScale_ZeroPausesAndResumes_KeepsExplicitPause() {
            SoundAsset _sound = MakeSound(3f, _followTimeScale: true);
            AudioHandle _explicit = Audio.Play(_sound);
            AudioHandle _other = Audio.Play(_sound);
            yield return null;
            _explicit.Pause();

            Time.timeScale = 0f;
            yield return null;
            Assert.IsFalse(VoiceOf(_other).Source.isPlaying, "timeScale 0 must pause the voice.");

            Time.timeScale = 1f;
            yield return null;
            Assert.IsTrue(VoiceOf(_other).Source.isPlaying, "timeScale > 0 must resume the voice.");
            Assert.IsTrue(_explicit.IsPaused, "An explicit pause must survive the time-scale pause.");
            Assert.IsFalse(VoiceOf(_explicit).Source.isPlaying);
        }

        [UnityTest]
        public IEnumerator FollowTimeScale_Zero_DoesNotEnd() {
            AudioHandle _handle = Audio.Play(MakeSound(0.1f, _followTimeScale: true));
            Time.timeScale = 0f;
            yield return WaitReal(0.3f);
            Assert.IsTrue(_handle.IsValid);
        }

        [UnityTest]
        public IEnumerator FollowTimeScale_Zero_DoesNotResumeGlobalPause() {
            AudioHandle _handle = Audio.Play(MakeSound(3f, _followTimeScale: true));
            yield return null;
            Audio.Pause();
            Time.timeScale = 0f;
            yield return null;
            Time.timeScale = 1f;
            yield return null;
            Assert.IsTrue(_handle.IsPaused);
            Assert.IsFalse(VoiceOf(_handle).Source.isPlaying);
        }

        // ==================== DUCKING ===================
        [UnityTest]
        public IEnumerator Ducking_VoicePlaying_LowersMusicAfterAttack() {
            Rebuild(new DuckingRule(AudioBus.Voice, AudioBus.Music, -6f, 0.1f, 0.2f));
            Audio.Play(MakeSound(3f, AudioBus.Voice));
            yield return WaitReal(0.3f);
            Assert.AreEqual(-6f, MusicOffsetDb, 0.05f);
            Assert.AreEqual(0.501f, Audio.Runtime.Mixer.GetSourceMultiplier(AudioBus.Music), 0.01f);
        }

        [UnityTest]
        public IEnumerator Ducking_Attack_InterpolatesLinearlyInDb() {
            Rebuild(new DuckingRule(AudioBus.Voice, AudioBus.Music, -12f, 1f, 1f));
            Audio.Play(MakeSound(3f, AudioBus.Voice));
            yield return WaitReal(0.5f);
            float _db = MusicOffsetDb;
            Assert.Less(_db, -3f);
            Assert.Greater(_db, -10f);
        }

        [UnityTest]
        public IEnumerator Ducking_VoiceEnds_RestoresAfterRelease() {
            Rebuild(new DuckingRule(AudioBus.Voice, AudioBus.Music, -6f, 0.05f, 0.2f));
            AudioHandle _voice = Audio.Play(MakeSound(0.4f, AudioBus.Voice));
            yield return WaitFor(() => MusicOffsetDb <= -5.999f, 1f);
            Assert.AreEqual(-6f, MusicOffsetDb, 0.05f);

            yield return WaitFor(() => !_voice.IsValid, 2f);
            Assert.IsFalse(_voice.IsValid);
            yield return WaitReal(0.5f);
            Assert.AreEqual(0f, MusicOffsetDb, 0.01f);
        }

        [UnityTest]
        public IEnumerator Ducking_TwoRulesSameTarget_UsesMostNegative() {
            Rebuild(
                new DuckingRule(AudioBus.Voice, AudioBus.Music, -6f, 0.05f, 0.2f),
                new DuckingRule(AudioBus.SFX, AudioBus.Music, -12f, 0.05f, 0.2f));
            Audio.Play(MakeSound(3f, AudioBus.Voice));
            Audio.Play(MakeSound(3f, AudioBus.SFX));
            yield return WaitReal(0.3f);
            Assert.AreEqual(-12f, MusicOffsetDb, 0.05f);
        }

        [UnityTest]
        public IEnumerator Ducking_PausedTriggerVoice_DoesNotDuck() {
            Rebuild(new DuckingRule(AudioBus.Voice, AudioBus.Music, -6f, 0.05f, 0.1f));
            Audio.Play(MakeSound(3f, AudioBus.Voice));
            Audio.Pause();
            yield return WaitReal(0.3f);
            Assert.AreEqual(0f, MusicOffsetDb, 0.01f);
        }

        [UnityTest]
        public IEnumerator Ducking_TimePausedTriggerVoice_Releases() {
            Rebuild(new DuckingRule(AudioBus.Voice, AudioBus.Music, -6f, 0.05f, 0.1f));
            Audio.Play(MakeSound(3f, AudioBus.Voice, _followTimeScale: true));
            yield return WaitFor(() => MusicOffsetDb <= -5.999f, 1f);
            Assert.AreEqual(-6f, MusicOffsetDb, 0.05f);
            Time.timeScale = 0f;
            yield return WaitReal(0.4f);
            Assert.AreEqual(0f, MusicOffsetDb, 0.01f);
        }

        [UnityTest]
        public IEnumerator Ducking_ListenerPausedTriggerVoice_Releases() {
            Rebuild(new DuckingRule(AudioBus.Voice, AudioBus.Music, -6f, 0.05f, 0.1f));
            Audio.Play(MakeSound(3f, AudioBus.Voice));
            yield return WaitFor(() => MusicOffsetDb <= -5.999f, 1f);
            AudioListener.pause = true;
            yield return WaitReal(0.4f);
            Assert.AreEqual(0f, MusicOffsetDb, 0.01f);
        }

        [UnityTest]
        public IEnumerator Ducking_VoiceInDelay_DoesNotDuck() {
            Rebuild(new DuckingRule(AudioBus.Voice, AudioBus.Music, -6f, 0.05f, 0.1f));
            SoundAsset _sound = MakeSound(1f, AudioBus.Voice);
            _sound.SetDelay(1f);
            Audio.Play(_sound);
            yield return WaitReal(0.3f);
            Assert.AreEqual(0f, MusicOffsetDb, 0.01f);
        }

        [UnityTest]
        public IEnumerator Ducking_ExternalActivity_CountsAsTrigger() {
            Rebuild(new DuckingRule(AudioBus.Voice, AudioBus.Music, -6f, 0.05f, 0.1f));
            Audio.Runtime.Ducker.SetExternalActivity(AudioBus.Voice, true);
            yield return WaitReal(0.3f);
            Assert.AreEqual(-6f, MusicOffsetDb, 0.05f);
            Audio.Runtime.Ducker.SetExternalActivity(AudioBus.Voice, false);
            yield return WaitReal(0.4f);
            Assert.AreEqual(0f, MusicOffsetDb, 0.01f);
        }

        [UnityTest]
        public IEnumerator Ducking_Tick_ZeroGC() {
            Rebuild(new DuckingRule(AudioBus.Voice, AudioBus.Music, -6f, 0.1f, 0.2f), new DuckingRule(AudioBus.SFX, AudioBus.Music, -3f, 0.1f, 0.2f));
            SoundAsset _voiceSound = MakeSound(3f, AudioBus.Voice);
            for (int _i = 0; _i < 3; _i++) Audio.Play(_voiceSound);
            yield return null;
            AudioRuntime _runtime = Audio.Runtime;
            Action _tick = () => {
                for (int _i = 0; _i < 100; _i++) _runtime.Ducker.Tick(0.016f, _runtime.Voices);
            };
            _tick();
            Assert.Less(MusicOffsetDb, -5.9f);
            int _count = CountAllocations(_tick);
            Assert.AreEqual(0, _count, "The ducker tick allocated managed memory.");
        }

        [Test]
        public void Ducking_Tick_PositiveControl() {
            object _sink = null;
            Action _allocate = () => { for (int _i = 0; _i < 10; _i++) _sink = new byte[64]; };
            Assert.Greater(CountAllocations(_allocate), 0);
            Assert.IsNotNull(_sink);
        }

        [UnityTest]
        public IEnumerator Pause_Refresh_ZeroGC() {
            SoundAsset _sound = MakeSound(3f);
            for (int _i = 0; _i < 4; _i++) Audio.Play(_sound);
            yield return null;
            VoiceManager _voices = Audio.Runtime.Voices;
            Action _toggle = () => {
                for (int _i = 0; _i < 20; _i++) {
                    _voices.SetPaused(true);
                    _voices.SetPaused(false);
                    _voices.Tick(0.001f);
                }
            };
            _toggle();
            Assert.AreEqual(0, CountAllocations(_toggle), "Pausing and resuming allocated managed memory.");
        }
    }
}
