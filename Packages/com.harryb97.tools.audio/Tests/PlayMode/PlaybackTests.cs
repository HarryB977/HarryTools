using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using HarryTools.GameLoop;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HarryTools.Audio.Tests {
    public class PlaybackTests {
        // ==================== VARIABLES ===================
        private const string TestKey = "HarryTools.Audio.Tests.Playback";

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

        private SoundAsset MakeSound(float _seconds, int _priority = 50) {
            var _asset = ScriptableObject.CreateInstance<SoundAsset>();
            _asset.SetClips(new[] { new ClipEntry(MakeClip(_seconds)) });
            _asset.SetPriority(_priority);
            created.Add(_asset);
            return _asset;
        }

        private static AudioVoice VoiceOf(AudioHandle _handle) {
            Assert.IsTrue(Audio.Runtime.Voices.TryGetVoice(_handle, out AudioVoice _voice), "Handle has no voice.");
            return _voice;
        }

        private static IEnumerator WaitFor(Func<bool> _condition, float _timeout) {
            float _end = Time.realtimeSinceStartup + _timeout;
            while (!_condition() && Time.realtimeSinceStartup < _end) yield return null;
        }

        // ==================== TESTS ===================
        [UnityTest]
        public IEnumerator Play_ReturnsValidHandle_AndPlays() {
            int _started = 0;
            Action<AudioHandle> _onStarted = _ => _started++;
            Audio.Started += _onStarted;
            SoundAsset _sound = MakeSound(2f);
            AudioHandle _handle = Audio.Play(_sound);
            Audio.Started -= _onStarted;

            Assert.IsTrue(_handle.IsValid);
            Assert.AreSame(_sound, _handle.Asset);
            Assert.AreEqual(1, _started);
            yield return null;
            Assert.IsTrue(_handle.IsPlaying);
            Assert.IsFalse(_handle.IsPaused);
        }

        [UnityTest]
        public IEnumerator Play_Ends_ReleasesVoiceAndFiresEnded() {
            int _ended = 0;
            AudioHandle _endedHandle = AudioHandle.Invalid;
            int _callback = 0;
            Action<AudioHandle> _onEnded = _h => { _ended++; _endedHandle = _h; };
            Audio.Ended += _onEnded;
            AudioHandle _handle = Audio.Play(MakeSound(0.1f)).OnEnded(_ => _callback++);
            yield return WaitFor(() => _ended > 0, 2f);
            Audio.Ended -= _onEnded;

            Assert.AreEqual(1, _ended);
            Assert.AreEqual(1, _callback);
            Assert.AreEqual(_handle, _endedHandle);
            Assert.IsFalse(_handle.IsValid);
            Assert.AreEqual(0, Audio.Runtime.Voices.ActiveVoices.Count);
        }

        [UnityTest]
        public IEnumerator StaleHandle_DoesNotAffectNewPlayback() {
            AudioHandle _old = Audio.Play(MakeSound(0.1f));
            yield return WaitFor(() => !_old.IsValid, 2f);
            Assert.IsFalse(_old.IsValid);

            AudioHandle _new = Audio.Play(MakeSound(3f));
            Assert.AreEqual(_old.VoiceIndex, _new.VoiceIndex, "The new sound should reuse the released voice.");
            Assert.AreNotEqual(_old, _new);
            float _volume = VoiceOf(_new).Source.volume;

            _old.Stop(0f);
            _old.Pause();
            _old.SetVolume(0f);
            _old.At(new Vector3(9f, 9f, 9f));
            yield return null;

            Assert.IsTrue(_new.IsPlaying);
            Assert.IsFalse(_new.IsPaused);
            Assert.AreEqual(_volume, VoiceOf(_new).Source.volume, 0.0001f);
            Assert.AreNotEqual(new Vector3(9f, 9f, 9f), VoiceOf(_new).transform.position);
        }

        [UnityTest]
        public IEnumerator Cooldown_BlocksRapidReplay() {
            SoundAsset _sound = MakeSound(0.5f);
            _sound.SetLimits(0.2f, 0, InstanceLimitPolicy.StealOldest);
            Assert.IsTrue(Audio.Play(_sound).IsValid);
            Assert.IsFalse(Audio.Play(_sound).IsValid);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.IsTrue(Audio.Play(_sound).IsValid);
        }

        [Test]
        public void MaxInstances_StealOldest() {
            SoundAsset _sound = MakeSound(3f);
            _sound.SetLimits(0f, 2, InstanceLimitPolicy.StealOldest);
            AudioHandle _a = Audio.Play(_sound);
            AudioHandle _b = Audio.Play(_sound);
            AudioHandle _c = Audio.Play(_sound);
            Assert.IsFalse(_a.IsValid);
            Assert.IsTrue(_b.IsValid);
            Assert.IsTrue(_c.IsValid);
            Assert.AreEqual(2, Audio.Runtime.Voices.ActiveVoices.Count);
        }

        [Test]
        public void MaxInstances_Ignore_ReturnsInvalid() {
            SoundAsset _sound = MakeSound(3f);
            _sound.SetLimits(0f, 2, InstanceLimitPolicy.Ignore);
            AudioHandle _a = Audio.Play(_sound);
            AudioHandle _b = Audio.Play(_sound);
            AudioHandle _c = Audio.Play(_sound);
            Assert.IsTrue(_a.IsValid);
            Assert.IsTrue(_b.IsValid);
            Assert.IsFalse(_c.IsValid);
            Assert.AreEqual(AudioHandle.Invalid, _c);
        }

        [Test]
        public void MaxInstances_StealQuietest() {
            SoundAsset _sound = MakeSound(3f);
            _sound.SetLimits(0f, 2, InstanceLimitPolicy.StealQuietest);
            AudioHandle _loud = Audio.Play(_sound);
            AudioHandle _quiet = Audio.Play(_sound).SetVolume(0.1f);
            AudioHandle _third = Audio.Play(_sound);
            Assert.IsTrue(_loud.IsValid);
            Assert.IsFalse(_quiet.IsValid);
            Assert.IsTrue(_third.IsValid);
        }

        [Test]
        public void PoolFull_StealsLowestScore_NotHigherPriority() {
            SoundAsset _low = MakeSound(3f, 10);
            SoundAsset _high = MakeSound(3f, 90);

            var _lows = new AudioHandle[4];
            for (int _i = 0; _i < 4; _i++) _lows[_i] = Audio.Play(_low);
            AudioHandle _important = Audio.Play(_high);
            Assert.IsTrue(_important.IsValid);
            Assert.AreEqual(4, Audio.Runtime.Voices.ActiveVoices.Count);
            int _stolen = 0;
            for (int _i = 0; _i < 4; _i++) if (!_lows[_i].IsValid) _stolen++;
            Assert.AreEqual(1, _stolen);

            Audio.StopAll();
            var _highs = new AudioHandle[4];
            for (int _i = 0; _i < 4; _i++) _highs[_i] = Audio.Play(_high);
            Assert.IsFalse(Audio.Play(_low).IsValid);
            for (int _i = 0; _i < 4; _i++) Assert.IsTrue(_highs[_i].IsValid);
        }

        [UnityTest]
        public IEnumerator Follow_UpdatesPosition() {
            SoundAsset _sound = MakeSound(3f);
            _sound.SetSpatial(SpatialSettings.Default3D);
            var _target = new GameObject("Follow Target");
            AudioHandle _handle = Audio.Play(_sound, _target.transform);
            _target.transform.position = new Vector3(5f, 0f, 0f);
            yield return null;
            yield return null;
            Assert.AreEqual(new Vector3(5f, 0f, 0f), VoiceOf(_handle).transform.position);
            Object.Destroy(_target);
        }

        [UnityTest]
        public IEnumerator FollowTarget_Destroyed_KeepsLastPosition() {
            SoundAsset _sound = MakeSound(3f);
            _sound.SetSpatial(SpatialSettings.Default3D);
            var _target = new GameObject("Follow Target");
            AudioHandle _handle = Audio.Play(_sound, _target.transform);
            _target.transform.position = new Vector3(3f, 0f, 0f);
            yield return null;
            yield return null;
            Object.Destroy(_target);
            yield return null;
            yield return null;
            Assert.IsTrue(_handle.IsPlaying);
            Assert.AreEqual(new Vector3(3f, 0f, 0f), VoiceOf(_handle).transform.position);
        }

        [Test]
        public void Stop_Zero_ReleasesImmediately() {
            AudioHandle _handle = Audio.Play(MakeSound(3f));
            _handle.Stop(0f);
            Assert.IsFalse(_handle.IsValid);
            Assert.AreEqual(0, Audio.Runtime.Voices.ActiveVoices.Count);
        }

        [UnityTest]
        public IEnumerator Stop_Default_UsesAssetFadeOut() {
            SoundAsset _sound = MakeSound(3f);
            _sound.SetFades(0f, 0.3f);
            AudioHandle _handle = Audio.Play(_sound);
            _handle.Stop();
            Assert.IsTrue(_handle.IsValid, "Stop() should fade with the asset FadeOut, not stop at once.");
            yield return WaitFor(() => !_handle.IsValid, 2f);
            Assert.IsFalse(_handle.IsValid);
        }

        [UnityTest]
        public IEnumerator Stop_WithFade_RampsDownThenEnds() {
            AudioHandle _handle = Audio.Play(MakeSound(3f));
            yield return null;
            float _initial = VoiceOf(_handle).Source.volume;
            _handle.Stop(0.5f);
            yield return new WaitForSecondsRealtime(0.25f);
            float _middle = VoiceOf(_handle).Source.volume;
            Assert.Greater(_middle, 0f);
            Assert.Less(_middle, _initial);
            yield return WaitFor(() => !_handle.IsValid, 2f);
            Assert.IsFalse(_handle.IsValid);
        }

        [UnityTest]
        public IEnumerator FadeIn_RampsUp() {
            SoundAsset _sound = MakeSound(3f);
            _sound.SetFades(0.5f, 0f);
            AudioHandle _handle = Audio.Play(_sound);
            Assert.AreEqual(0f, VoiceOf(_handle).Source.volume, 0.0001f);
            yield return new WaitForSecondsRealtime(0.25f);
            float _middle = VoiceOf(_handle).Source.volume;
            Assert.Greater(_middle, 0f);
            Assert.Less(_middle, 1f);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(1f, VoiceOf(_handle).Source.volume, 0.0001f);
        }

        [UnityTest]
        public IEnumerator WaitForEnd_CompletesWhenFinished() => UniTask.ToCoroutine(async () => {
            AudioHandle _handle = Audio.Play(MakeSound(0.1f));
            await _handle.WaitForEnd().Timeout(TimeSpan.FromSeconds(2));
            Assert.IsFalse(_handle.IsValid);
        });

        [UnityTest]
        public IEnumerator WaitForEnd_Cancelled_Throws() => UniTask.ToCoroutine(async () => {
            AudioHandle _handle = Audio.Play(MakeSound(3f));
            var _cts = new CancellationTokenSource();
            UniTask _wait = _handle.WaitForEnd(_cts.Token);
            _cts.Cancel();
            bool _thrown = false;
            try {
                await _wait;
            } catch (OperationCanceledException) {
                _thrown = true;
            }
            Assert.IsTrue(_thrown);
            Assert.IsTrue(_handle.IsValid);
        });

        [UnityTest]
        public IEnumerator SceneUnload_WithFollowingVoices_NoErrors() {
            Scene _scene = SceneManager.CreateScene("HarryTools Audio Test " + Guid.NewGuid().ToString("N"));
            var _a = new GameObject("Emitter A");
            var _b = new GameObject("Emitter B");
            SceneManager.MoveGameObjectToScene(_a, _scene);
            SceneManager.MoveGameObjectToScene(_b, _scene);
            SoundAsset _sound = MakeSound(0.4f);
            _sound.SetSpatial(SpatialSettings.Default3D);
            AudioHandle _ha = Audio.Play(_sound, _a.transform);
            AudioHandle _hb = Audio.Play(_sound, _b.transform);
            yield return null;

            yield return SceneManager.UnloadSceneAsync(_scene);
            yield return WaitFor(() => !_ha.IsValid && !_hb.IsValid, 3f);

            Assert.IsFalse(_ha.IsValid);
            Assert.IsFalse(_hb.IsValid);
            Assert.AreEqual(0, Audio.Runtime.Voices.ActiveVoices.Count);
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator NoMixer_SourceVolume_UsesBusMultiplier() {
            AudioPlatform.OverrideUsesMixer(false);
            Audio.SetVolume(AudioBus.Master, 0.5f);
            Audio.SetVolume(AudioBus.SFX, 0.5f);
            AudioHandle _handle = Audio.Play(MakeSound(3f));
            Assert.AreEqual(0.25f, VoiceOf(_handle).Source.volume, 0.0001f);
            Assert.IsNull(VoiceOf(_handle).Source.outputAudioMixerGroup);

            Audio.SetMuted(AudioBus.SFX, true);
            yield return null;
            yield return null;
            Assert.AreEqual(0f, VoiceOf(_handle).Source.volume, 0.0001f);
        }

        [Test]
        public void HandleSetVolume_ScalesSource() {
            AudioHandle _handle = Audio.Play(MakeSound(3f)).SetVolume(0.5f);
            Assert.AreEqual(0.5f, VoiceOf(_handle).Source.volume, 0.0001f);
        }

        [UnityTest]
        public IEnumerator PauseResume_KeepsVoiceAlive() {
            AudioHandle _handle = Audio.Play(MakeSound(0.2f));
            _handle.Pause();
            Assert.IsTrue(_handle.IsPaused);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.IsTrue(_handle.IsValid, "A paused voice must not be released.");
            _handle.Resume();
            Assert.IsFalse(_handle.IsPaused);
            yield return WaitFor(() => !_handle.IsValid, 2f);
            Assert.IsFalse(_handle.IsValid);
        }

        [Test]
        public void Runtime_RegistersLoop_AndRemovesItOnShutdown() {
            Assert.IsTrue(LoopSystem.HasLoop("HarryTools.Audio"));
            Assert.IsNotNull(GameObject.Find("HarryTools Audio"));
            Audio.ShutdownForTests();
            Assert.IsFalse(LoopSystem.HasLoop("HarryTools.Audio"));
        }

        [Test]
        public void Runtime_IsCreatedLazilyOnFirstUse() {
            Audio.ShutdownForTests();
            Assert.IsNull(Audio.Current);
            Assert.IsNotNull(Audio.Runtime);
            Assert.IsTrue(LoopSystem.HasLoop("HarryTools.Audio"));
        }

        [Test]
        public void Volume_IsSavedAndRestoredOnNextRuntime() {
            Audio.SetVolume(AudioBus.Voice, 0.3f);
            Audio.SetMuted(AudioBus.UI, true);
            Audio.InitializeForTests(settings);
            Assert.AreEqual(0.3f, Audio.GetVolume(AudioBus.Voice), 0.0001f);
            Assert.IsTrue(Audio.IsMuted(AudioBus.UI));
        }

        [Test]
        public void Listener_DefaultsToActiveAudioListener_AndCanBeOverridden() {
            Assert.AreSame(listener.transform, Audio.Listener);
            var _other = new GameObject("Other Listener Target");
            Audio.SetListener(_other.transform);
            Assert.AreSame(_other.transform, Audio.Listener);
            Audio.SetListener(null);
            Assert.AreSame(listener.transform, Audio.Listener);
            Object.Destroy(_other);
        }

        [UnityTest]
        public IEnumerator Delay_WaitsBeforePlaying_AndDoesNotEndEarly() {
            SoundAsset _sound = MakeSound(0.2f);
            _sound.SetDelay(0.3f);
            AudioHandle _handle = Audio.Play(_sound);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.IsTrue(_handle.IsValid, "A delayed voice must stay alive during its delay.");
            yield return WaitFor(() => !_handle.IsValid, 2f);
            Assert.IsFalse(_handle.IsValid);
        }

        [Test]
        public void PlaySoundRef_ResolvesThroughActiveCatalog() {
            SoundAsset _sound = MakeSound(3f);
            _sound.SetId("0123456789abcdef0123456789abcdef");
            var _catalog = ScriptableObject.CreateInstance<AudioCatalog>();
            created.Add(_catalog);
            _catalog.SetItems(new List<AudioCatalogItem> { _sound });
            AudioCatalog.Active = _catalog;
            try {
                AudioHandle _handle = Audio.Play(new SoundRef(_sound.Id), Vector3.one);
                Assert.IsTrue(_handle.IsValid);
                Assert.AreSame(_sound, _handle.Asset);
            } finally {
                AudioCatalog.Active = null;
            }
        }

        [Test]
        public void Extension_PlaysSound() {
            AudioHandle _handle = MakeSound(3f).Play();
            Assert.IsTrue(_handle.IsValid);
        }

        [Test]
        public void InvalidHandle_OperationsDoNothing() {
            AudioHandle _handle = AudioHandle.Invalid;
            Assert.IsFalse(_handle.IsValid);
            Assert.IsFalse(_handle.IsPlaying);
            Assert.IsNull(_handle.Asset);
            _handle.Stop();
            _handle.Pause();
            _handle.Resume();
            _handle.SetVolume(0.5f).WithPitch(1f, 1f).WithVolume(1f, 1f).At(Vector3.zero).Follow(null);
        }
    }
}
