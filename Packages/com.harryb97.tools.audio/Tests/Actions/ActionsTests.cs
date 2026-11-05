using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using HarryTools.Actions;
using HarryTools.Audio.Actions;
using HarryTools.Conditions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HarryTools.Audio.Tests {
    public class ActionsTests {
        // ==================== VARIABLES ===================
        private const string TestKey = "HarryTools.Audio.Tests.Actions";

        private readonly List<Object> created = new List<Object>();
        private AudioProjectSettings settings;
        private GameObject listener;

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
        }

        [TearDown]
        public void TearDown() {
            Audio.ShutdownForTests();
            PlayerPrefs.DeleteKey(TestKey);
            if (listener != null) Object.DestroyImmediate(listener);
            for (int _i = 0; _i < created.Count; _i++) {
                if (created[_i] != null) Object.DestroyImmediate(created[_i]);
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

        private SoundAsset MakeSound(float _seconds) {
            var _asset = ScriptableObject.CreateInstance<SoundAsset>();
            _asset.SetClips(new[] { new ClipEntry(MakeClip(_seconds)) });
            _asset.SetSpatial(SpatialSettings.Default3D);
            created.Add(_asset);
            return _asset;
        }

        private VoiceAsset MakeVoice(float _seconds) {
            var _voice = ScriptableObject.CreateInstance<VoiceAsset>();
            _voice.SetClips(new[] { new ClipEntry(MakeClip(_seconds)) });
            created.Add(_voice);
            return _voice;
        }

        private static IEnumerator WaitFor(Func<bool> _condition, float _timeout) {
            float _end = Time.realtimeSinceStartup + _timeout;
            while (!_condition() && Time.realtimeSinceStartup < _end) yield return null;
        }

        private static UniTask Run(IAction _action, CancellationToken _token = default) => _action.Run(LogicContext.Empty, _token);

        // ==================== TESTS ===================
        [UnityTest]
        public IEnumerator PlayVoiceAction_WaitsUntilLineEnds() {
            var _action = new PlayVoiceAction(MakeVoice(0.4f), null, true);
            float _start = Time.realtimeSinceStartup;
            UniTask _task = Run(_action);
            yield return null;
            yield return null;
            Assert.AreEqual(UniTaskStatus.Pending, _task.Status);
            yield return WaitFor(() => _task.Status != UniTaskStatus.Pending, 3f);
            Assert.AreEqual(UniTaskStatus.Succeeded, _task.Status);
            Assert.GreaterOrEqual(Time.realtimeSinceStartup - _start, 0.3f);
        }

        [UnityTest]
        public IEnumerator PlayVoiceAction_NoWait_CompletesAtOnce() {
            var _action = new PlayVoiceAction(MakeVoice(1f), null, false);
            UniTask _task = Run(_action);
            Assert.AreEqual(UniTaskStatus.Succeeded, _task.Status);
            yield return null;
            Assert.AreEqual(1, Audio.Runtime.Voices.ActiveVoices.Count);
        }

        [UnityTest]
        public IEnumerator PlayConversationAction_Cancelled_StopsConversation() {
            var _conversation = ScriptableObject.CreateInstance<ConversationAsset>();
            _conversation.SetLines(new[] {
                new ConversationLine { Voice = MakeVoice(1f), PauseAfter = 0f },
                new ConversationLine { Voice = MakeVoice(1f), PauseAfter = 0f }
            });
            created.Add(_conversation);
            int _started = 0;
            Action<VoiceLineInfo> _onStarted = _i => _started++;
            Audio.Voices.LineStarted += _onStarted;

            var _cts = new CancellationTokenSource();
            UniTask _task = Run(new PlayConversationAction(_conversation, true), _cts.Token);
            yield return null;
            yield return null;
            Assert.AreEqual(UniTaskStatus.Pending, _task.Status);
            Assert.AreEqual(1, _started);

            _cts.Cancel();
            yield return null;
            yield return null;
            Assert.AreEqual(UniTaskStatus.Canceled, _task.Status);

            yield return WaitFor(() => Audio.Runtime.Voices.ActiveVoices.Count == 0, 1f);
            Assert.AreEqual(0, Audio.Runtime.Voices.ActiveVoices.Count);
            Assert.AreEqual(1, _started, "The second line must not start after cancelling.");
            Audio.Voices.LineStarted -= _onStarted;
            _cts.Dispose();
        }

        [UnityTest]
        public IEnumerator PlaySoundAction_Follow_FollowsTarget() {
            var _target = new GameObject("Target");
            created.Add(_target);
            AudioHandle _handle = default;
            Action<AudioHandle> _onStarted = _h => _handle = _h;
            Audio.Started += _onStarted;
            var _action = new PlaySoundAction(MakeSound(2f), new TargetComponent<Transform>(TargetSource.Custom, _target.transform), true, false);
            Run(_action).Forget();
            Audio.Started -= _onStarted;
            Assert.IsTrue(_handle.IsValid);

            _target.transform.position = new Vector3(6f, 0f, 0f);
            yield return null;
            yield return null;
            Assert.IsTrue(Audio.Runtime.Voices.TryGetVoice(_handle, out AudioVoice _voice));
            Assert.AreEqual(new Vector3(6f, 0f, 0f), _voice.transform.position);
        }

        [UnityTest]
        public IEnumerator PlaySoundAction_NoFollow_StaysAtPosition() {
            var _target = new GameObject("Target");
            _target.transform.position = new Vector3(2f, 0f, 0f);
            created.Add(_target);
            AudioHandle _handle = default;
            Action<AudioHandle> _onStarted = _h => _handle = _h;
            Audio.Started += _onStarted;
            Run(new PlaySoundAction(MakeSound(2f), new TargetComponent<Transform>(TargetSource.Custom, _target.transform), false, false)).Forget();
            Audio.Started -= _onStarted;

            _target.transform.position = new Vector3(9f, 0f, 0f);
            yield return null;
            yield return null;
            Assert.IsTrue(Audio.Runtime.Voices.TryGetVoice(_handle, out AudioVoice _voice));
            Assert.AreEqual(new Vector3(2f, 0f, 0f), _voice.transform.position);
        }

        [UnityTest]
        public IEnumerator PlaySoundAction_CancelWhileWaiting_StopsSound() {
            var _cts = new CancellationTokenSource();
            UniTask _task = Run(new PlaySoundAction(MakeSound(3f), null, false, true), _cts.Token);
            yield return null;
            Assert.AreEqual(UniTaskStatus.Pending, _task.Status);
            _cts.Cancel();
            yield return null;
            yield return null;
            Assert.AreEqual(UniTaskStatus.Canceled, _task.Status);
            Assert.AreEqual(0, Audio.Runtime.Voices.ActiveVoices.Count);
            _cts.Dispose();
        }

        [Test]
        public void SetBusVolumeAction_SetsVolume() {
            Run(new SetBusVolumeAction(AudioBus.SFX, 0.25f));
            Assert.AreEqual(0.25f, Audio.GetVolume(AudioBus.SFX), 0.001f);
        }

        [Test]
        public void PauseAndResumeActions_TogglePause() {
            Run(new PauseAudioAction(true, AudioBus.SFX));
            Assert.IsTrue(Audio.IsPaused);
            Run(new ResumeAudioAction(true, AudioBus.SFX));
            Assert.IsFalse(Audio.IsPaused);
            Run(new PauseAudioAction(false, AudioBus.SFX));
            Assert.IsTrue(Audio.IsBusPaused(AudioBus.SFX));
            Run(new ResumeAudioAction(false, AudioBus.SFX));
            Assert.IsFalse(Audio.IsBusPaused(AudioBus.SFX));
        }

        [UnityTest]
        public IEnumerator PlayAndStopMusicActions() {
            var _music = ScriptableObject.CreateInstance<MusicAsset>();
            _music.SetClips(new[] { new ClipEntry(MakeClip(2f)) });
            created.Add(_music);
            Run(new PlayMusicAction(_music, 0f));
            Assert.AreSame(_music, Audio.Music.Current);
            Run(new StopMusicAction(0f));
            yield return null;
            Assert.IsNull(Audio.Music.Current);
        }

        [UnityTest]
        public IEnumerator StopSoundAction_StopsInstances() {
            SoundAsset _sound = MakeSound(3f);
            AudioHandle _handle = Audio.Play(_sound);
            Run(new StopSoundAction(_sound, 0f));
            yield return null;
            Assert.IsFalse(_handle.IsValid);
        }
    }
}
