using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HarryTools.Audio.Tests {
    public class SoundPlayerTests {
        // ==================== VARIABLES ===================
        private const string TestKey = "HarryTools.Audio.Tests.SoundPlayer";

        private readonly List<Object> created = new List<Object>();
        private AudioProjectSettings settings;
        private GameObject listener;
        private int startedCount;
        private System.Action<AudioHandle> onStarted;

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
            startedCount = 0;
            onStarted = _h => startedCount++;
            Audio.Started += onStarted;
        }

        [TearDown]
        public void TearDown() {
            Audio.Started -= onStarted;
            Audio.ShutdownForTests();
            PlayerPrefs.DeleteKey(TestKey);
            if (listener != null) Object.DestroyImmediate(listener);
            for (int _i = 0; _i < created.Count; _i++) {
                if (created[_i] != null) Object.DestroyImmediate(created[_i]);
            }
            created.Clear();
        }

        // ==================== HELPERS ===================
        private SoundAsset MakeSound(float _seconds, float _fadeOut = 0f) {
            int _samples = Mathf.CeilToInt(44100 * _seconds);
            var _clip = AudioClip.Create("Sine", _samples, 1, 44100, false);
            var _data = new float[_samples];
            for (int _i = 0; _i < _samples; _i++) _data[_i] = 0.05f * Mathf.Sin(2f * Mathf.PI * 440f * _i / 44100f);
            _clip.SetData(_data, 0);
            created.Add(_clip);
            var _asset = ScriptableObject.CreateInstance<SoundAsset>();
            _asset.SetClips(new[] { new ClipEntry(_clip) });
            _asset.SetFades(0f, _fadeOut);
            created.Add(_asset);
            return _asset;
        }

        private SoundPlayer MakePlayer(SoundAsset _sound, SoundPlayerTrigger _trigger, bool _stopOnDisable = true) {
            var _go = new GameObject("Player");
            _go.SetActive(false);
            var _player = _go.AddComponent<SoundPlayer>();
            _player.Sound = _sound;
            _player.PlayOn = _trigger;
            _player.StopOnDisable = _stopOnDisable;
            created.Add(_go);
            return _player;
        }

        // ==================== TESTS ===================
        [UnityTest]
        public IEnumerator OnEnable_Plays() {
            SoundPlayer _player = MakePlayer(MakeSound(2f), SoundPlayerTrigger.OnEnable);
            _player.gameObject.SetActive(true);
            Assert.AreEqual(1, startedCount);
            yield return null;
            Assert.AreEqual(1, Audio.Runtime.Voices.ActiveVoices.Count);
        }

        [UnityTest]
        public IEnumerator OnStart_PlaysInStart() {
            SoundPlayer _player = MakePlayer(MakeSound(2f), SoundPlayerTrigger.OnStart);
            _player.gameObject.SetActive(true);
            Assert.AreEqual(0, startedCount);
            yield return null;
            Assert.AreEqual(1, startedCount);
        }

        [UnityTest]
        public IEnumerator Disable_StopsWhenStopOnDisable() {
            SoundPlayer _player = MakePlayer(MakeSound(2f), SoundPlayerTrigger.OnEnable);
            _player.gameObject.SetActive(true);
            yield return null;
            Assert.AreEqual(1, Audio.Runtime.Voices.ActiveVoices.Count);
            _player.gameObject.SetActive(false);
            Assert.AreEqual(0, Audio.Runtime.Voices.ActiveVoices.Count);
        }

        [UnityTest]
        public IEnumerator Disable_KeepsPlayingWhenNotStopOnDisable() {
            SoundPlayer _player = MakePlayer(MakeSound(2f), SoundPlayerTrigger.OnEnable, false);
            _player.gameObject.SetActive(true);
            yield return null;
            _player.gameObject.SetActive(false);
            yield return null;
            Assert.AreEqual(1, Audio.Runtime.Voices.ActiveVoices.Count);
        }

        [UnityTest]
        public IEnumerator Manual_DoesNotAutoPlay() {
            SoundPlayer _player = MakePlayer(MakeSound(2f), SoundPlayerTrigger.Manual);
            _player.gameObject.SetActive(true);
            yield return null;
            Assert.AreEqual(0, startedCount);
            _player.Play();
            Assert.AreEqual(1, startedCount);
        }

        [UnityTest]
        public IEnumerator Play_WhilePlaying_Restarts() {
            SoundPlayer _player = MakePlayer(MakeSound(2f), SoundPlayerTrigger.Manual);
            _player.gameObject.SetActive(true);
            _player.Play();
            _player.Play();
            yield return null;
            Assert.AreEqual(2, startedCount);
            Assert.AreEqual(1, Audio.Runtime.Voices.ActiveVoices.Count);
        }

        [UnityTest]
        public IEnumerator FollowSelf_FollowsTransform() {
            SoundAsset _sound = MakeSound(2f);
            _sound.SetSpatial(SpatialSettings.Default3D);
            SoundPlayer _player = MakePlayer(_sound, SoundPlayerTrigger.OnEnable);
            _player.gameObject.SetActive(true);
            _player.transform.position = new Vector3(4f, 0f, 0f);
            yield return null;
            yield return null;
            AudioVoice _voice = null;
            foreach (AudioVoice _v in Audio.Runtime.Voices.ActiveVoices) _voice = _v;
            Assert.IsNotNull(_voice);
            Assert.AreEqual(new Vector3(4f, 0f, 0f), _voice.transform.position);
        }
    }
}
