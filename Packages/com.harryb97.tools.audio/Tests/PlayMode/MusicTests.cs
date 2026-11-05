using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HarryTools.Audio.Tests {
    public class MusicTests {
        // ==================== VARIABLES ===================
        private const string TestKey = "HarryTools.Audio.Tests.Music";
        private const int Frequency = 44100;

        private readonly List<Object> created = new List<Object>();
        private AudioProjectSettings settings;
        private GameObject listener;

        // ==================== SETUP ===================
        [SetUp]
        public void SetUp() {
            PlayerPrefs.DeleteKey(TestKey);
            listener = new GameObject("Test Listener", typeof(AudioListener));
            settings = ScriptableObject.CreateInstance<AudioProjectSettings>();
            settings.MaxVoices = 4;
            settings.PrewarmVoices = 4;
            settings.PersistenceKey = TestKey;
            settings.DefaultCrossfade = 1f;
            settings.SetDucking(new List<DuckingRule>());
            created.Add(settings);
            Audio.InitializeForTests(settings);
            Audio.SetListener(listener.transform);
        }

        [TearDown]
        public void TearDown() {
            AudioListener.pause = false;
            Audio.Resume();
            Audio.Resume(AudioBus.Music);
            Audio.ShutdownForTests();
            AudioPlatform.OverrideUsesMixer(null);
            PlayerPrefs.DeleteKey(TestKey);
            if (listener != null) Object.DestroyImmediate(listener);
            for (int _i = 0; _i < created.Count; _i++) {
                if (created[_i] == null) continue;
                if (created[_i] is GameObject) Object.DestroyImmediate(created[_i]);
                else Object.Destroy(created[_i]);
            }
            created.Clear();
        }

        // ==================== HELPERS ===================
        private AudioClip MakeClip(float _seconds, float _hz = 440f) {
            int _samples = Mathf.CeilToInt(Frequency * _seconds);
            var _clip = AudioClip.Create("Sine", _samples, 1, Frequency, false);
            var _data = new float[_samples];
            for (int _i = 0; _i < _samples; _i++) _data[_i] = 0.05f * Mathf.Sin(2f * Mathf.PI * _hz * _i / Frequency);
            _clip.SetData(_data, 0);
            created.Add(_clip);
            return _clip;
        }

        private MusicAsset MakeMusic(float _loopSeconds, float _introSeconds = 0f) {
            var _asset = ScriptableObject.CreateInstance<MusicAsset>();
            _asset.SetClips(new[] { new ClipEntry(MakeClip(_loopSeconds)) });
            if (_introSeconds > 0f) _asset.SetIntro(MakeClip(_introSeconds, 660f));
            created.Add(_asset);
            return _asset;
        }

        private static MusicChannel Music => Audio.Music;
        private static MusicDeck CurrentDeck => Music.GetDeck(Music.CurrentDeckIndex);

        private static bool AnyPlaying(MusicDeck _deck) => _deck.GetSource(0).isPlaying || _deck.GetSource(1).isPlaying;

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

        // Fails with a clear message when the Editor audio does not advance (muted Editor or no output device), instead of faking timings.
        private static void AssertAudioAdvances(AudioSource _source, int _before) {
            Assert.Greater(_source.timeSamples, _before, "AudioSource.timeSamples did not advance: is the Editor audio muted or the output device missing?");
        }

        private MusicZone MakeZone(Vector3 _center, MusicAsset _music, int _priority = 0, float _crossfade = 0f) {
            var _go = new GameObject("Zone", typeof(BoxCollider));
            _go.transform.position = _center;
            _go.GetComponent<BoxCollider>().size = new Vector3(4f, 4f, 4f);
            _go.GetComponent<BoxCollider>().isTrigger = true;
            var _zone = _go.AddComponent<MusicZone>();
            _zone.Music = _music;
            _zone.Priority = _priority;
            _zone.Crossfade = _crossfade;
            created.Add(_go);
            Physics.SyncTransforms();
            return _zone;
        }

        // ==================== PLAY ===================
        [UnityTest]
        public IEnumerator Play_StartsMusic_CurrentSet() {
            MusicAsset _music = MakeMusic(2f);
            int _changed = 0;
            MusicAsset _changedTo = null;
            Music.Changed += _m => { _changed++; _changedTo = _m; };

            Music.Play(_music, 0f);
            Assert.AreSame(_music, Music.Current);
            Assert.IsTrue(Music.IsPlaying);
            Assert.AreEqual(1, _changed);
            Assert.AreSame(_music, _changedTo);

            AudioSource _source = CurrentDeck.GetSource(0);
            yield return WaitFor(() => _source.timeSamples > 2000, 1f);
            AssertAudioAdvances(_source, 2000);
            Assert.IsTrue(_source.isPlaying);
            Assert.AreEqual(1f, _source.pitch);
            Assert.AreEqual(0f, _source.spatialBlend);
        }

        [UnityTest]
        public IEnumerator Play_SameTrack_NoRestart() {
            MusicAsset _music = MakeMusic(3f);
            int _changed = 0;
            Music.Changed += _ => _changed++;
            Music.Play(_music, 0f);
            AudioSource _source = CurrentDeck.GetSource(0);
            int _deck = Music.CurrentDeckIndex;
            yield return WaitFor(() => _source.timeSamples > 8000, 1f);
            AssertAudioAdvances(_source, 8000);
            int _before = _source.timeSamples;

            Music.Play(_music, 0f);
            Music.Play(_music);
            yield return WaitReal(0.1f);
            Assert.Greater(_source.timeSamples, _before, "Playing the same track restarted it.");
            Assert.AreEqual(_deck, Music.CurrentDeckIndex);
            Assert.AreEqual(1, _changed);
        }

        [UnityTest]
        public IEnumerator Play_MusicRef_Unresolved_DoesNothing() {
            Music.Play(new MusicRef("missing-id"));
            yield return null;
            Assert.IsNull(Music.Current);
        }

        [UnityTest]
        public IEnumerator Crossfade_BothDecksAudibleMidway_ThenOldStops() {
            MusicAsset _old = MakeMusic(4f);
            MusicAsset _new = MakeMusic(4f);
            Music.Play(_old, 0f);
            MusicDeck _oldDeck = CurrentDeck;
            yield return WaitReal(0.2f);

            Music.Play(_new, 1f);
            MusicDeck _newDeck = CurrentDeck;
            Assert.AreNotSame(_oldDeck, _newDeck);
            Assert.AreSame(_new, Music.Current);

            yield return WaitReal(0.5f);
            Assert.That(_oldDeck.Volume, Is.GreaterThan(0f).And.LessThan(1f), "Old deck midway.");
            Assert.That(_newDeck.Volume, Is.GreaterThan(0f).And.LessThan(1f), "New deck midway.");
            Assert.AreEqual(1f, _oldDeck.Volume + _newDeck.Volume, 0.1f, "Linear crossfade.");

            yield return WaitReal(0.7f);
            Assert.IsFalse(_oldDeck.IsActive, "The old deck should be stopped.");
            Assert.IsFalse(AnyPlaying(_oldDeck));
            Assert.AreEqual(1f, _newDeck.Volume, 0.001f);
            Assert.IsTrue(AnyPlaying(_newDeck));
        }

        [UnityTest]
        public IEnumerator Crossfade_ThirdTrackDuringCrossfade_UsesTwoDecks() {
            MusicAsset _a = MakeMusic(4f);
            MusicAsset _b = MakeMusic(4f);
            MusicAsset _c = MakeMusic(4f);
            Music.Play(_a, 0f);
            yield return null;
            Music.Play(_b, 1f);
            yield return WaitReal(0.2f);
            Music.Play(_c, 1f);
            Assert.AreSame(_c, Music.Current);
            int _active = 0;
            for (int _i = 0; _i < 2; _i++) if (Music.GetDeck(_i).IsActive) _active++;
            Assert.AreEqual(2, _active);
            yield return WaitReal(1.2f);
            Assert.AreSame(_c, CurrentDeck.Asset);
            Assert.IsFalse(Music.GetDeck(1 - Music.CurrentDeckIndex).IsActive);
        }

        [UnityTest]
        public IEnumerator Play_SameTrackWhileFadingOut_FadesBackWithoutRestart() {
            MusicAsset _music = MakeMusic(4f);
            Music.Play(_music, 0f);
            MusicDeck _deck = CurrentDeck;
            AudioSource _source = _deck.GetSource(0);
            yield return WaitFor(() => _source.timeSamples > 8000, 1f);
            AssertAudioAdvances(_source, 8000);

            Music.Stop(1f);
            Assert.IsNull(Music.Current);
            yield return WaitReal(0.3f);
            int _before = _source.timeSamples;
            Music.Play(_music, 0.3f);
            Assert.AreSame(_music, Music.Current);
            Assert.AreSame(_deck, CurrentDeck, "The fading deck should be brought back.");
            yield return WaitReal(0.5f);
            Assert.Greater(_source.timeSamples, _before, "Bringing the track back restarted it.");
            Assert.AreEqual(1f, _deck.Volume, 0.001f);
            Assert.IsTrue(_deck.IsActive);
        }

        [UnityTest]
        public IEnumerator Stop_FadesOut_ClearsCurrent_FiresChanged() {
            MusicAsset _music = MakeMusic(3f);
            Music.Play(_music, 0f);
            MusicDeck _deck = CurrentDeck;
            int _changed = 0;
            MusicAsset _changedTo = _music;
            Music.Changed += _m => { _changed++; _changedTo = _m; };
            yield return WaitReal(0.2f);

            Music.Stop(0.3f);
            Assert.IsNull(Music.Current);
            Assert.IsFalse(Music.IsPlaying);
            Assert.AreEqual(1, _changed);
            Assert.IsNull(_changedTo);
            Assert.IsTrue(_deck.IsActive, "Still fading out.");
            yield return WaitReal(0.5f);
            Assert.IsFalse(_deck.IsActive);
            Assert.IsFalse(AnyPlaying(_deck));
        }

        [UnityTest]
        public IEnumerator Stop_Zero_StopsAtOnce() {
            Music.Play(MakeMusic(3f), 0f);
            MusicDeck _deck = CurrentDeck;
            yield return null;
            Music.Stop(0f);
            Assert.IsFalse(_deck.IsActive);
            Assert.IsFalse(AnyPlaying(_deck));
        }

        [UnityTest]
        public IEnumerator Changed_ThrowingSubscriber_IsIsolated() {
            Music.Changed += _ => throw new InvalidOperationException("test");
            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("test"));
            MusicAsset _music = MakeMusic(2f);
            Assert.DoesNotThrow(() => Music.Play(_music, 0f));
            Assert.AreSame(_music, Music.Current);
            yield return null;
        }

        // ==================== INTRO AND LOOP ===================
        [UnityTest]
        public IEnumerator IntroThenLoop_LoopScheduledAtIntroEnd() {
            MusicAsset _music = MakeMusic(2f, 0.2f);
            Music.Play(_music, 0f);
            MusicDeck _deck = CurrentDeck;
            AudioSource _intro = _deck.GetSource(0);
            AudioSource _loop = _deck.GetSource(1);
            Assert.AreSame(_music.Intro, _intro.clip);
            Assert.AreSame(_music.Clips[0].Clip, _loop.clip);
            Assert.IsTrue(_loop.loop);

            yield return WaitReal(0.45f);
            Assert.IsTrue(_loop.isPlaying, "The loop should be playing after the intro.");
            Assert.IsFalse(_intro.isPlaying, "The intro should have ended.");
            AssertAudioAdvances(_loop, 0);
        }

        [UnityTest]
        public IEnumerator IntroThenLoop_SampleAccurate() {
            MusicAsset _music = MakeMusic(2f, 0.25f);
            Music.Play(_music, 0f);
            MusicDeck _deck = CurrentDeck;
            AudioClip _introClip = _music.Intro;

            double _introStart = _deck.GetStart(0);
            double _introEnd = _deck.GetEnd(0);
            double _loopStart = _deck.GetStart(1);
            double _scheduledGap = (_loopStart - _introEnd) * Frequency;
            Assert.AreEqual(_introClip.samples / (double)_introClip.frequency, _introEnd - _introStart, 1e-9);
            Assert.AreEqual(0d, _scheduledGap, 1d, "Scheduled gap between the intro end and the loop start (samples).");

            // Observed: once the loop runs, its position must match the time elapsed since its scheduled start (within one DSP buffer).
            AudioSource _loop = _deck.GetSource(1);
            yield return WaitFor(() => AudioSettings.dspTime > _loopStart + 0.3, 2f);
            double _dsp = AudioSettings.dspTime;
            int _position = _loop.timeSamples;
            AssertAudioAdvances(_loop, 0);
            AudioSettings.GetDSPBufferSize(out int _bufferLength, out _);
            double _expected = (_dsp - _loopStart) * Frequency;
            double _drift = _position - _expected;
            double _tolerance = 2.0 * _bufferLength * Frequency / AudioSettings.outputSampleRate;
            Debug.Log($"[MusicTests] Intro->loop: scheduled gap {_scheduledGap:0.###} samples; loop position {_position} vs expected {_expected:0.#} from dspTime (drift {_drift:0.#} samples, tolerance {_tolerance:0.#}, buffer {_bufferLength} @ {AudioSettings.outputSampleRate} Hz).");
            Assert.AreEqual(0d, _drift, _tolerance);
        }

        [UnityTest]
        public IEnumerator LoopPoints_WrapsToLoopStart() {
            MusicAsset _music = MakeMusic(1f);
            _music.SetLoopPoints(true, 0.2, 0.5);
            Music.Play(_music, 0f);
            MusicDeck _deck = CurrentDeck;

            yield return WaitReal(0.8f);
            int _playing = 0;
            for (int _i = 0; _i < 2; _i++) {
                AudioSource _source = _deck.GetSource(_i);
                if (!_source.isPlaying) continue;
                _playing++;
                Assert.That(_source.time, Is.InRange(0.19f, 0.51f), "Source " + _i + " left the loop range.");
            }
            Assert.Greater(_playing, 0, "No source is playing.");

            // Keep sampling over a few more wraps: no source ever plays past LoopEnd.
            float _end = Time.realtimeSinceStartup + 1.2f;
            while (Time.realtimeSinceStartup < _end) {
                for (int _i = 0; _i < 2; _i++) {
                    AudioSource _source = _deck.GetSource(_i);
                    if (_source.isPlaying) Assert.That(_source.time, Is.InRange(0.19f, 0.51f));
                }
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator NoLoop_EndsAndClearsCurrent() {
            MusicAsset _music = MakeMusic(0.3f);
            _music.SetLoop(false);
            MusicAsset _changedTo = _music;
            Music.Changed += _m => _changedTo = _m;
            Music.Play(_music, 0f);
            Assert.AreSame(_music, Music.Current);
            yield return WaitReal(0.2f);
            Assert.AreSame(_music, Music.Current, "Ended too early.");
            yield return WaitFor(() => Music.Current == null, 2f);
            Assert.IsNull(Music.Current);
            Assert.IsNull(_changedTo);
        }

        [UnityTest]
        public IEnumerator NoMixer_IntroThenLoop_UsesPlay() {
            AudioPlatform.OverrideUsesMixer(false);
            Audio.InitializeForTests(settings);
            Audio.SetVolume(AudioBus.Music, 0.5f);
            MusicAsset _music = MakeMusic(2f, 0.2f);
            _music.SetLoopPoints(true, 0.5, 1.0);
            Music.Play(_music, 0f);
            MusicDeck _deck = CurrentDeck;
            AudioSource _intro = _deck.GetSource(0);
            AudioSource _loop = _deck.GetSource(1);
            Assert.IsTrue(_intro.isPlaying);
            Assert.IsFalse(_loop.isPlaying, "Without mixer the loop must not be scheduled ahead.");
            yield return null;
            Assert.AreEqual(0.5f, _intro.volume, 0.01f, "Bus volume multiplied on the source.");
            Assert.IsNull(_intro.outputAudioMixerGroup);

            yield return WaitFor(() => _loop.isPlaying, 1f);
            Assert.IsTrue(_loop.isPlaying);
            Assert.IsFalse(_intro.isPlaying);
            Assert.IsTrue(_loop.loop, "Loop points are ignored without mixer: full loop.");
            yield return WaitReal(1.2f);
            Assert.Greater(_loop.time, 1.05f, "Loop points must be ignored without mixer.");
        }

        // ==================== PAUSE ===================
        [UnityTest]
        public IEnumerator Pause_PausesMusic() {
            MusicAsset _music = MakeMusic(3f);
            Music.Play(_music, 0f);
            MusicDeck _deck = CurrentDeck;
            AudioSource _source = _deck.GetSource(0);
            yield return WaitFor(() => _source.timeSamples > 8000, 1f);
            AssertAudioAdvances(_source, 8000);

            Music.Pause();
            Assert.IsFalse(Music.IsPlaying);
            Assert.IsFalse(AnyPlaying(_deck));
            int _atPause = _source.timeSamples;
            yield return WaitReal(0.3f);
            Assert.IsFalse(AnyPlaying(_deck));
            Assert.AreSame(_music, Music.Current);

            Music.Resume();
            Assert.IsTrue(Music.IsPlaying);
            yield return WaitReal(0.3f);
            Assert.IsTrue(_source.isPlaying);
            Assert.Greater(_source.timeSamples, _atPause, "Resume restarted the track.");
            Assert.Less(_source.timeSamples, _atPause + Frequency, "Resume jumped ahead.");
        }

        [UnityTest]
        public IEnumerator Pause_GlobalAndBus_PauseMusic_IgnorePauseKeepsPlaying() {
            MusicAsset _music = MakeMusic(3f);
            Music.Play(_music, 0f);
            MusicDeck _deck = CurrentDeck;
            yield return WaitReal(0.2f);

            Audio.Pause();
            yield return null;
            Assert.IsFalse(Music.IsPlaying);
            Assert.IsFalse(AnyPlaying(_deck));
            Audio.Resume();
            yield return WaitReal(0.2f);
            Assert.IsTrue(AnyPlaying(_deck));

            Audio.Pause(AudioBus.Music);
            yield return null;
            Assert.IsFalse(AnyPlaying(_deck));
            Audio.Resume(AudioBus.Music);
            yield return WaitReal(0.2f);
            Assert.IsTrue(AnyPlaying(_deck));

            MusicAsset _ignore = MakeMusic(3f);
            _ignore.SetPauseOptions(true, false);
            Music.Play(_ignore, 0f);
            MusicDeck _ignoreDeck = CurrentDeck;
            Assert.IsTrue(_ignoreDeck.GetSource(0).ignoreListenerPause);
            Audio.Pause();
            yield return WaitReal(0.2f);
            Assert.IsTrue(Music.IsPlaying);
            Assert.IsTrue(AnyPlaying(_ignoreDeck));
        }

        [UnityTest]
        public IEnumerator Pause_FreezesFades() {
            MusicAsset _music = MakeMusic(3f);
            Music.Play(_music, 1f);
            MusicDeck _deck = CurrentDeck;
            yield return WaitReal(0.3f);
            Music.Pause();
            float _level = _deck.Volume;
            Assert.That(_level, Is.GreaterThan(0f).And.LessThan(1f));
            yield return WaitReal(0.3f);
            Assert.AreEqual(_level, _deck.Volume, 0.0001f);
        }

        [UnityTest]
        public IEnumerator Pause_DuringIntro_LoopStillStartsAfterIntro() {
            MusicAsset _music = MakeMusic(2f, 0.4f);
            Music.Play(_music, 0f);
            MusicDeck _deck = CurrentDeck;
            AudioSource _intro = _deck.GetSource(0);
            AudioSource _loop = _deck.GetSource(1);
            yield return WaitFor(() => _intro.timeSamples > 4000, 1f);
            AssertAudioAdvances(_intro, 4000);

            Music.Pause();
            yield return WaitReal(0.6f);
            Assert.IsFalse(_loop.isPlaying, "The loop started while paused.");

            Music.Resume();
            yield return WaitReal(0.1f);
            Assert.AreEqual(0, _loop.timeSamples, "The loop started before the rest of the intro.");
            yield return WaitFor(() => _loop.timeSamples > 0, 1.5f);
            Assert.Greater(_loop.timeSamples, 0, "The loop never started after resuming.");
            yield return WaitReal(0.1f);
            Assert.IsFalse(_intro.isPlaying);
            double _gap = (_deck.GetStart(1) - _deck.GetEnd(0)) * Frequency;
            Assert.AreEqual(0d, _gap, 1d, "Rescheduled loop must still start exactly at the intro end.");
        }

        [UnityTest]
        public IEnumerator Play_WhilePaused_StartsOnResume() {
            Audio.Pause();
            MusicAsset _music = MakeMusic(2f, 0.2f);
            Music.Play(_music, 0f);
            MusicDeck _deck = CurrentDeck;
            Assert.AreSame(_music, Music.Current);
            Assert.IsFalse(Music.IsPlaying);
            yield return WaitReal(0.3f);
            Assert.IsFalse(AnyPlaying(_deck));

            Audio.Resume();
            yield return WaitReal(0.6f);
            Assert.IsTrue(_deck.GetSource(1).isPlaying, "Loop after the intro.");
            Assert.IsFalse(_deck.GetSource(0).isPlaying);
            Assert.AreEqual(0d, (_deck.GetStart(1) - _deck.GetEnd(0)) * Frequency, 1d);
        }

        [UnityTest]
        public IEnumerator LoopPoints_PauseResume_StaysInRange() {
            MusicAsset _music = MakeMusic(1f);
            _music.SetLoopPoints(true, 0.2, 0.5);
            Music.Play(_music, 0f);
            MusicDeck _deck = CurrentDeck;
            yield return WaitReal(0.75f);
            Music.Pause();
            Assert.IsFalse(AnyPlaying(_deck));
            yield return WaitReal(0.3f);
            Music.Resume();

            int _playingFrames = 0;
            float _end = Time.realtimeSinceStartup + 1.2f;
            while (Time.realtimeSinceStartup < _end) {
                for (int _i = 0; _i < 2; _i++) {
                    AudioSource _source = _deck.GetSource(_i);
                    if (!_source.isPlaying) continue;
                    _playingFrames++;
                    Assert.That(_source.time, Is.InRange(0.19f, 0.51f), "Source " + _i + " left the loop range after resuming.");
                }
                yield return null;
            }
            Assert.Greater(_playingFrames, 0);
        }

        [UnityTest]
        public IEnumerator ListenerPause_FreezesMusic_UnlessIgnorePause() {
            MusicAsset _music = MakeMusic(3f);
            Music.Play(_music, 0f);
            MusicDeck _deck = CurrentDeck;
            yield return WaitReal(0.2f);
            AudioListener.pause = true;
            yield return null;
            Assert.IsTrue(_deck.IsPaused);
            AudioListener.pause = false;
            yield return WaitReal(0.2f);
            Assert.IsFalse(_deck.IsPaused);
            Assert.IsTrue(AnyPlaying(_deck));
        }

        // ==================== DUCKING ===================
        [UnityTest]
        public IEnumerator Music_CountsAsDuckingTrigger() {
            settings.SetDucking(new List<DuckingRule> { new DuckingRule(AudioBus.Music, AudioBus.SFX, -6f, 0.05f, 0.1f) });
            Audio.InitializeForTests(settings);
            Music.Play(MakeMusic(3f), 0f);
            yield return WaitReal(0.3f);
            Assert.AreEqual(-6f, Audio.Runtime.Mixer.GetDuckOffsetDb(AudioBus.SFX), 0.05f);
            Music.Pause();
            yield return WaitReal(0.4f);
            Assert.AreEqual(0f, Audio.Runtime.Mixer.GetDuckOffsetDb(AudioBus.SFX), 0.01f);
        }

        // ==================== ZONES ===================
        [UnityTest]
        public IEnumerator MusicZone_EnterExit_SwitchesAndRestores() {
            MusicAsset _base = MakeMusic(4f);
            MusicAsset _zoneMusic = MakeMusic(4f);
            MakeZone(new Vector3(20f, 0f, 0f), _zoneMusic);
            Music.Play(_base, 0f);
            yield return WaitReal(0.4f);
            Assert.AreSame(_base, Music.Current);

            listener.transform.position = new Vector3(20f, 1f, 0f);
            yield return WaitReal(0.5f);
            Assert.AreSame(_zoneMusic, Music.Current);

            listener.transform.position = Vector3.zero;
            yield return WaitReal(0.5f);
            Assert.AreSame(_base, Music.Current);
        }

        [UnityTest]
        public IEnumerator MusicZone_NothingBefore_StopsOnExit() {
            MusicAsset _zoneMusic = MakeMusic(4f);
            MakeZone(new Vector3(20f, 0f, 0f), _zoneMusic);
            listener.transform.position = new Vector3(20f, 0f, 0f);
            yield return WaitReal(0.5f);
            Assert.AreSame(_zoneMusic, Music.Current);
            listener.transform.position = Vector3.zero;
            yield return WaitReal(0.5f);
            Assert.IsNull(Music.Current);
        }

        [UnityTest]
        public IEnumerator MusicZone_HigherPriorityWins() {
            MusicAsset _low = MakeMusic(4f);
            MusicAsset _high = MakeMusic(4f);
            MusicAsset _later = MakeMusic(4f);
            MakeZone(new Vector3(20f, 0f, 0f), _high, 5);
            MakeZone(new Vector3(21f, 0f, 0f), _low, 1);

            listener.transform.position = new Vector3(20.5f, 0f, 0f);
            yield return WaitReal(0.5f);
            Assert.AreSame(_high, Music.Current, "Higher priority wins whatever the order.");

            // Same priority: the zone entered last wins.
            MakeZone(new Vector3(20.5f, 0f, 0f), _later, 5);
            yield return WaitReal(0.5f);
            Assert.AreSame(_later, Music.Current);
        }

        [UnityTest]
        public IEnumerator MusicZone_Disabled_LeavesZone() {
            MusicAsset _base = MakeMusic(4f);
            MusicAsset _zoneMusic = MakeMusic(4f);
            MusicZone _zone = MakeZone(Vector3.zero, _zoneMusic);
            Music.Play(_base, 0f);
            yield return WaitReal(0.5f);
            Assert.AreSame(_zoneMusic, Music.Current);
            _zone.enabled = false;
            yield return WaitReal(0.5f);
            Assert.AreSame(_base, Music.Current);
        }

        // ==================== ALLOCATIONS ===================
        [UnityTest]
        public IEnumerator Tick_SteadyState_ZeroGC() {
            MusicAsset _music = MakeMusic(1f, 0.2f);
            _music.SetLoopPoints(true, 0.2, 0.6);
            MakeZone(new Vector3(20f, 0f, 0f), MakeMusic(1f));
            MakeZone(new Vector3(-20f, 0f, 0f), MakeMusic(1f));
            Music.Play(_music, 0.5f);
            yield return WaitReal(0.3f);
            MusicChannel _channel = Music;
            Action _tick = () => {
                for (int _i = 0; _i < 100; _i++) _channel.Tick(0.016f);
            };
            _tick();
            Assert.AreEqual(0, CountAllocations(_tick), "The music tick allocated managed memory.");
        }

        [Test]
        public void Tick_PositiveControl() {
            object _sink = null;
            Action _allocate = () => { for (int _i = 0; _i < 10; _i++) _sink = new byte[64]; };
            Assert.Greater(CountAllocations(_allocate), 0);
            Assert.IsNotNull(_sink);
        }
    }
}
