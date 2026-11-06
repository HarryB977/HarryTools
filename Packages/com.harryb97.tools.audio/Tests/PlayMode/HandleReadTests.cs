using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HarryTools.Audio.Tests {
    public class HandleReadTests {
        // ==================== VARIABLES ===================
        private const string TestKey = "HarryTools.Audio.Tests.HandleRead";

        private readonly List<Object> created = new List<Object>();
        private AudioProjectSettings settings;
        private GameObject listener;
        private Action<AudioHandle> onStarted;
        private Action<AudioHandle> onEnded;
        private int startedCount;
        private int endedCount;

        // ==================== SETUP ===================
        [SetUp]
        public void SetUp() {
            PlayerPrefs.DeleteKey(TestKey);
            listener = new GameObject("Test Listener", typeof(AudioListener));
            settings = ScriptableObject.CreateInstance<AudioProjectSettings>();
            settings.MaxVoices = 8;
            settings.PrewarmVoices = 8;
            settings.PersistenceKey = TestKey;
            created.Add(settings);
            Audio.InitializeForTests(settings);
            startedCount = 0;
            endedCount = 0;
            onStarted = _ => startedCount++;
            onEnded = _ => endedCount++;
        }

        [TearDown]
        public void TearDown() {
            Audio.Started -= onStarted;
            Audio.Ended -= onEnded;
            Audio.ShutdownForTests();
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
        private AudioClip MakeClip(float _seconds) {
            int _samples = Mathf.CeilToInt(44100 * _seconds);
            var _clip = AudioClip.Create("Sine", _samples, 1, 44100, false);
            var _data = new float[_samples];
            for (int _i = 0; _i < _samples; _i++) _data[_i] = 0.05f * Mathf.Sin(2f * Mathf.PI * 440f * _i / 44100f);
            _clip.SetData(_data, 0);
            created.Add(_clip);
            return _clip;
        }

        private SoundAsset MakeSound(params AudioClip[] _clips) {
            var _asset = ScriptableObject.CreateInstance<SoundAsset>();
            var _entries = new ClipEntry[_clips.Length];
            for (int _i = 0; _i < _clips.Length; _i++) _entries[_i] = new ClipEntry(_clips[_i]);
            _asset.SetClips(_entries);
            created.Add(_asset);
            return _asset;
        }

        private VoiceAsset MakeVoice(float _seconds) {
            var _speaker = ScriptableObject.CreateInstance<SpeakerAsset>();
            created.Add(_speaker);
            var _voice = ScriptableObject.CreateInstance<VoiceAsset>();
            _voice.SetClips(new[] { new ClipEntry(MakeClip(_seconds)) });
            _voice.SetSpeaker(_speaker);
            _voice.SetSpatial(SpatialSettings.Default2D);
            created.Add(_voice);
            return _voice;
        }

        private static IEnumerator WaitReal(float _seconds) {
            float _end = Time.realtimeSinceStartup + _seconds;
            while (Time.realtimeSinceStartup < _end) yield return null;
        }

        private static IEnumerator WaitFor(Func<bool> _condition, float _timeout) {
            float _end = Time.realtimeSinceStartup + _timeout;
            while (!_condition() && Time.realtimeSinceStartup < _end) yield return null;
        }

        // ==================== TESTS ===================
        [Test]
        public void Clip_IsTheSelectedClip() {
            AudioClip _first = MakeClip(1f);
            AudioClip _second = MakeClip(1.5f);
            SoundAsset _sound = MakeSound(_first, _second);
            _sound.SetSelection(ClipSelection.Sequence);

            AudioHandle _a = Audio.Play(_sound);
            AudioHandle _b = Audio.Play(_sound);

            Assert.AreSame(_first, _a.Clip);
            Assert.AreSame(_second, _b.Clip);
        }

        [Test]
        public void Duration_UsesPitch() {
            SoundAsset _sound = MakeSound(MakeClip(1f));
            _sound.SetPitchRange(new Vector2(2f, 2f));
            AudioHandle _handle = Audio.Play(_sound);
            Assert.AreEqual(0.5f, _handle.Duration, 0.01f);
        }

        [Test]
        public void Duration_DoesNotIncludeDelay() {
            SoundAsset _sound = MakeSound(MakeClip(1f));
            _sound.SetDelay(0.5f);
            Assert.AreEqual(1f, Audio.Play(_sound).Duration, 0.01f);
        }

        [Test]
        public void IsLooping_And_Is3D() {
            SoundAsset _looping3D = MakeSound(MakeClip(1f));
            _looping3D.SetLoop(true);
            _looping3D.SetSpatial(SpatialSettings.Default3D);
            SoundAsset _plain = MakeSound(MakeClip(1f));
            _plain.SetSpatial(SpatialSettings.Default2D);

            AudioHandle _a = Audio.Play(_looping3D);
            AudioHandle _b = Audio.Play(_plain);

            Assert.IsTrue(_a.IsLooping);
            Assert.IsTrue(_a.Is3D);
            Assert.IsFalse(_b.IsLooping);
            Assert.IsFalse(_b.Is3D);
        }

        [UnityTest]
        public IEnumerator Position_FollowsTarget() {
            var _go = new GameObject("Target");
            created.Add(_go);
            _go.transform.position = new Vector3(1f, 2f, 3f);
            AudioHandle _handle = Audio.Play(MakeSound(MakeClip(2f)), _go.transform);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), _handle.Position);

            _go.transform.position = new Vector3(5f, 0f, -2f);
            yield return null;
            yield return null;
            Assert.AreEqual(new Vector3(5f, 0f, -2f), _handle.Position);
        }

        [UnityTest]
        public IEnumerator Elapsed_ExcludesDelayAndPause() {
            SoundAsset _sound = MakeSound(MakeClip(3f));
            _sound.SetDelay(0.3f);
            AudioHandle _handle = Audio.Play(_sound);

            yield return WaitReal(0.2f);
            Assert.AreEqual(0f, _handle.Elapsed, "Elapsed must not count the delay.");

            yield return WaitReal(0.6f);
            float _running = _handle.Elapsed;
            Assert.Greater(_running, 0.1f);
            Assert.Less(_running, 0.6f);

            _handle.Pause();
            yield return null;
            float _atPause = _handle.Elapsed;
            yield return WaitReal(0.3f);
            Assert.AreEqual(_atPause, _handle.Elapsed, 0.05f, "Elapsed must not grow while paused.");

            _handle.Resume();
            yield return WaitReal(0.2f);
            Assert.Greater(_handle.Elapsed, _atPause + 0.05f);
        }

        [UnityTest]
        public IEnumerator InDelay_TrueUntilTheAssetDelayElapses() {
            SoundAsset _delayed = MakeSound(MakeClip(3f));
            _delayed.SetDelay(0.4f);
            SoundAsset _plain = MakeSound(MakeClip(3f));
            AudioHandle _handle = Audio.Play(_delayed);
            AudioHandle _immediate = Audio.Play(_plain);

            Assert.IsTrue(_handle.InDelay, "A sound with a delay starts in its delay.");
            Assert.IsFalse(_immediate.InDelay, "A sound without a delay is never in delay.");
            yield return WaitReal(0.2f);
            Assert.IsTrue(_handle.InDelay);

            yield return WaitReal(0.4f);
            Assert.IsFalse(_handle.InDelay, "InDelay must turn false once the delay elapsed.");
            Assert.IsTrue(_handle.IsValid);
        }

        [UnityTest]
        public IEnumerator InDelay_FrozenWhilePaused() {
            SoundAsset _delayed = MakeSound(MakeClip(3f));
            _delayed.SetDelay(0.3f);
            AudioHandle _handle = Audio.Play(_delayed);
            _handle.Pause();
            yield return WaitReal(0.5f);
            Assert.IsTrue(_handle.InDelay, "The delay does not run while the playback is paused.");
        }

        [Test]
        public void InvalidHandle_ReturnsDefaults() {
            AudioHandle _handle = AudioHandle.Invalid;
            Assert.IsNull(_handle.Clip);
            Assert.AreEqual(0f, _handle.Duration);
            Assert.IsFalse(_handle.IsLooping);
            Assert.AreEqual(Vector3.zero, _handle.Position);
            Assert.IsFalse(_handle.Is3D);
            Assert.AreEqual(0f, _handle.Elapsed);
            Assert.IsFalse(_handle.InDelay);
        }

        [UnityTest]
        public IEnumerator StaleHandle_ReturnsDefaults() {
            AudioHandle _handle = Audio.Play(MakeSound(MakeClip(0.1f)));
            yield return WaitFor(() => !_handle.IsValid, 2f);
            Assert.IsFalse(_handle.InDelay);
            Assert.IsNull(_handle.Clip);
            Assert.AreEqual(0f, _handle.Duration);
            Assert.AreEqual(0f, _handle.Elapsed);
        }

        [UnityTest]
        public IEnumerator StartedEnded_OncePerPlayback() {
            Audio.Started += onStarted;
            Audio.Ended += onEnded;

            AudioHandle _single = Audio.Play(MakeSound(MakeClip(0.1f)));
            yield return WaitFor(() => !_single.IsValid, 2f);
            Assert.AreEqual(1, startedCount);
            Assert.AreEqual(1, endedCount);

            startedCount = 0;
            endedCount = 0;
            var _conversation = ScriptableObject.CreateInstance<ConversationAsset>();
            _conversation.SetLines(new[] {
                new ConversationLine { Voice = MakeVoice(0.1f), PauseAfter = 0.05f },
                new ConversationLine { Voice = MakeVoice(0.1f) }
            });
            created.Add(_conversation);
            ConversationHandle _handle = Audio.Voices.Play(_conversation);
            yield return WaitFor(() => !_handle.IsPlaying, 3f);
            yield return WaitReal(0.1f);
            Assert.AreEqual(2, startedCount);
            Assert.AreEqual(2, endedCount);
        }
    }
}
