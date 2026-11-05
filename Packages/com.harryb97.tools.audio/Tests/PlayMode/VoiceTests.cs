using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HarryTools.Audio.Tests {
    public class VoiceTests {
        // ==================== VARIABLES ===================
        private const string TestKey = "HarryTools.Audio.Tests.Voice";

        private readonly List<Object> created = new List<Object>();
        private readonly List<VoiceLineInfo> started = new List<VoiceLineInfo>();
        private readonly List<float> startTimes = new List<float>();
        private readonly List<VoiceLineInfo> ended = new List<VoiceLineInfo>();
        private readonly List<bool> endedInterrupted = new List<bool>();
        private readonly List<float> endTimes = new List<float>();
        private Action<VoiceLineInfo> onStarted;
        private Action<VoiceLineInfo, bool> onEnded;
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

            started.Clear();
            startTimes.Clear();
            ended.Clear();
            endedInterrupted.Clear();
            endTimes.Clear();
            onStarted = _info => {
                started.Add(_info);
                startTimes.Add(Time.realtimeSinceStartup);
            };
            onEnded = (_info, _interrupted) => {
                ended.Add(_info);
                endedInterrupted.Add(_interrupted);
                endTimes.Add(Time.realtimeSinceStartup);
            };
            Audio.Voices.LineStarted += onStarted;
            Audio.Voices.LineEnded += onEnded;
        }

        [TearDown]
        public void TearDown() {
            VoiceChannel _channel = Audio.Current?.VoiceChannel;
            if (_channel != null) {
                _channel.LineStarted -= onStarted;
                _channel.LineEnded -= onEnded;
            }
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

        private SpeakerAsset MakeSpeaker(string _name) {
            var _speaker = ScriptableObject.CreateInstance<SpeakerAsset>();
            _speaker.SetDisplayName(_name);
            created.Add(_speaker);
            return _speaker;
        }

        private VoiceAsset MakeVoice(SpeakerAsset _speaker, float _seconds, bool _3d = true) {
            var _voice = ScriptableObject.CreateInstance<VoiceAsset>();
            _voice.SetClips(new[] { new ClipEntry(MakeClip(_seconds)) });
            _voice.SetSpeaker(_speaker);
            _voice.SetSpatial(_3d ? SpatialSettings.Default3D : SpatialSettings.Default2D);
            created.Add(_voice);
            return _voice;
        }

        private ConversationAsset MakeConversation(params ConversationLine[] _lines) {
            var _conversation = ScriptableObject.CreateInstance<ConversationAsset>();
            _conversation.SetLines(_lines);
            created.Add(_conversation);
            return _conversation;
        }

        private AudioEmitter MakeEmitter(SpeakerAsset _speaker, Vector3 _position, EmitterBusyPolicy _policy = EmitterBusyPolicy.Queue) {
            var _go = new GameObject("Emitter " + (_speaker != null ? _speaker.DisplayName : "none"));
            _go.transform.position = _position;
            var _emitter = _go.AddComponent<AudioEmitter>();
            _emitter.Speaker = _speaker;
            _emitter.BusyPolicy = _policy;
            created.Add(_go);
            return _emitter;
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
        public IEnumerator PlayVoice_FiresStartedAndEnded() {
            VoiceAsset _voice = MakeVoice(MakeSpeaker("A"), 0.4f, false);
            _voice.SetPitchRange(new Vector2(2f, 2f));
            AudioHandle _handle = Audio.Voices.Play(_voice);

            Assert.IsTrue(_handle.IsValid);
            Assert.AreEqual(1, started.Count);
            Assert.AreSame(_voice, started[0].Voice);
            Assert.AreSame(_voice.Speaker, started[0].Speaker);
            Assert.AreEqual(_handle, started[0].Handle);
            Assert.AreEqual(0.2f, started[0].Duration, 0.01f);

            yield return WaitFor(() => ended.Count > 0, 2f);
            Assert.AreEqual(1, ended.Count);
            Assert.IsFalse(endedInterrupted[0]);
            Assert.AreEqual(_handle, ended[0].Handle);
            Assert.AreEqual(0.2f, endTimes[0] - startTimes[0], 0.1f);
        }

        [UnityTest]
        public IEnumerator LineStarted_WaitsForDelay() {
            VoiceAsset _voice = MakeVoice(MakeSpeaker("A"), 0.3f, false);
            _voice.SetDelay(0.3f);
            float _start = Time.realtimeSinceStartup;
            AudioHandle _handle = Audio.Voices.Play(_voice);
            Assert.IsTrue(_handle.IsValid);
            Assert.AreEqual(0, started.Count, "LineStarted must wait for the delay.");
            yield return WaitFor(() => started.Count > 0, 2f);
            Assert.AreEqual(1, started.Count);
            Assert.AreEqual(0.3f, startTimes[0] - _start, 0.1f);
        }

        [UnityTest]
        public IEnumerator Voice_UsesActiveEmitterOfSpeaker_AndFollowsIt() {
            SpeakerAsset _speaker = MakeSpeaker("A");
            AudioEmitter _older = MakeEmitter(_speaker, new Vector3(-5f, 0f, 0f));
            AudioEmitter _newer = MakeEmitter(_speaker, new Vector3(5f, 0f, 0f));
            Assert.IsTrue(AudioEmitter.TryGetActive(_speaker, out AudioEmitter _active));
            Assert.AreSame(_newer, _active);

            AudioHandle _handle = Audio.Voices.Play(MakeVoice(_speaker, 2f));
            Assert.AreEqual(1, started.Count);
            Assert.AreSame(_newer, started[0].Emitter);
            Assert.AreEqual(1f, VoiceOf(_handle).Source.spatialBlend, 0.001f);
            Assert.IsTrue(_newer.IsSpeaking);
            Assert.IsFalse(_older.IsSpeaking);

            _newer.transform.position = new Vector3(5f, 3f, 1f);
            yield return null;
            yield return null;
            Assert.AreEqual(new Vector3(5f, 3f, 1f), VoiceOf(_handle).transform.position);

            _newer.enabled = false;
            Assert.IsTrue(AudioEmitter.TryGetActive(_speaker, out _active));
            Assert.AreSame(_older, _active, "A disabled emitter is not active; the previous one wins.");
        }

        [Test]
        public void Voice_NoEmitter_Plays2D() {
            AudioHandle _handle = Audio.Voices.Play(MakeVoice(MakeSpeaker("Nobody"), 1f));
            Assert.IsTrue(_handle.IsValid);
            Assert.AreEqual(0f, VoiceOf(_handle).Source.spatialBlend);
            Assert.IsNull(started[0].Emitter);
        }

        [UnityTest]
        public IEnumerator Emitter_Queue_PlaysSequentially() {
            SpeakerAsset _speaker = MakeSpeaker("A");
            AudioEmitter _emitter = MakeEmitter(_speaker, Vector3.zero);
            AudioHandle _first = _emitter.Say(MakeVoice(_speaker, 0.3f));
            AudioHandle _second = _emitter.Say(MakeVoice(_speaker, 0.2f));

            Assert.IsTrue(_second.IsValid, "A queued handle is valid at once.");
            Assert.IsFalse(_second.IsPlaying);
            Assert.AreEqual(1, started.Count);
            yield return null;
            Assert.IsTrue(_first.IsPlaying);
            Assert.IsFalse(_second.IsPlaying);

            yield return WaitFor(() => ended.Count >= 2, 3f);
            Assert.AreEqual(2, started.Count);
            Assert.AreEqual(_first, started[0].Handle);
            Assert.AreEqual(_second, started[1].Handle);
            Assert.AreEqual(_first, ended[0].Handle);
            Assert.IsFalse(endedInterrupted[0]);
            Assert.GreaterOrEqual(startTimes[1], endTimes[0], "The second line must start after the first ends.");
            Assert.AreEqual(0.3f, startTimes[1] - startTimes[0], 0.1f);
        }

        [UnityTest]
        public IEnumerator Emitter_Interrupt_StopsCurrent() {
            SpeakerAsset _speaker = MakeSpeaker("A");
            AudioEmitter _emitter = MakeEmitter(_speaker, Vector3.zero, EmitterBusyPolicy.Interrupt);
            AudioHandle _first = _emitter.Say(MakeVoice(_speaker, 2f));
            yield return null;
            AudioHandle _second = _emitter.Say(MakeVoice(_speaker, 2f));

            Assert.AreEqual(1, ended.Count);
            Assert.AreEqual(_first, ended[0].Handle);
            Assert.IsTrue(endedInterrupted[0]);
            Assert.AreEqual(2, started.Count);
            Assert.AreEqual(_second, started[1].Handle);
            yield return null;
            Assert.IsTrue(_second.IsPlaying);
            yield return WaitFor(() => !_first.IsValid, 1f);
            Assert.IsFalse(_first.IsValid, "The interrupted line fades out in 0.1 s.");
            Assert.AreEqual(1, ended.Count, "The fade-out end must not raise LineEnded again.");
        }

        [UnityTest]
        public IEnumerator QueuedHandle_Stop_RemovesFromQueue() {
            SpeakerAsset _speaker = MakeSpeaker("A");
            AudioEmitter _emitter = MakeEmitter(_speaker, Vector3.zero);
            AudioHandle _a = _emitter.Say(MakeVoice(_speaker, 0.2f));
            AudioHandle _b = _emitter.Say(MakeVoice(_speaker, 0.2f));
            AudioHandle _c = _emitter.Say(MakeVoice(_speaker, 0.2f));

            _b.Stop();
            Assert.IsFalse(_b.IsValid);
            Assert.IsTrue(_c.IsValid);

            yield return WaitFor(() => ended.Count >= 2, 3f);
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.AreEqual(2, started.Count);
            Assert.AreEqual(_a, started[0].Handle);
            Assert.AreEqual(_c, started[1].Handle);
        }

        [UnityTest]
        public IEnumerator Conversation_PlaysLinesInOrder_WithPauses() {
            SpeakerAsset _speaker = MakeSpeaker("A");
            VoiceAsset _l1 = MakeVoice(_speaker, 0.1f, false);
            VoiceAsset _l2 = MakeVoice(_speaker, 0.1f, false);
            VoiceAsset _l3 = MakeVoice(_speaker, 0.1f, false);
            ConversationAsset _conversation = MakeConversation(
                new ConversationLine { Voice = _l1, PauseAfter = 0.1f },
                new ConversationLine { Voice = _l2, PauseAfter = 0.1f },
                new ConversationLine { Voice = _l3, PauseAfter = 0.1f });

            float _start = Time.realtimeSinceStartup;
            ConversationHandle _handle = Audio.Voices.Play(_conversation);
            Assert.IsTrue(_handle.IsPlaying);
            Assert.AreEqual(0, _handle.CurrentLine);
            yield return WaitFor(() => !_handle.IsPlaying, 3f);
            float _total = Time.realtimeSinceStartup - _start;

            Assert.AreEqual(3, started.Count);
            Assert.AreSame(_l1, started[0].Voice);
            Assert.AreSame(_l2, started[1].Voice);
            Assert.AreSame(_l3, started[2].Voice);
            Assert.AreEqual(0.6f, _total, 0.1f);
        }

        [UnityTest]
        public IEnumerator Conversation_Stop_EndsWithInterrupted() {
            SpeakerAsset _speaker = MakeSpeaker("A");
            ConversationAsset _conversation = MakeConversation(
                new ConversationLine { Voice = MakeVoice(_speaker, 1f, false) },
                new ConversationLine { Voice = MakeVoice(_speaker, 1f, false) });
            ConversationHandle _handle = Audio.Voices.Play(_conversation);
            AudioHandle _line = started[0].Handle;
            yield return new WaitForSecondsRealtime(0.2f);

            _handle.Stop();
            Assert.IsFalse(_handle.IsPlaying);
            Assert.AreEqual(1, ended.Count);
            Assert.IsTrue(endedInterrupted[0]);
            yield return WaitFor(() => !_line.IsValid, 1f);
            Assert.IsFalse(_line.IsValid, "The line fades out in 0.1 s.");
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.AreEqual(1, started.Count, "No line plays after Stop.");
            Assert.AreEqual(1, ended.Count);
        }

        [UnityTest]
        public IEnumerator Conversation_SpeakersAlternate_UseTheirEmitters() {
            SpeakerAsset _alice = MakeSpeaker("Alice");
            SpeakerAsset _bob = MakeSpeaker("Bob");
            AudioEmitter _aliceEmitter = MakeEmitter(_alice, new Vector3(-4f, 0f, 0f));
            AudioEmitter _bobEmitter = MakeEmitter(_bob, new Vector3(4f, 0f, 0f));
            var _positions = new List<Vector3>();
            Action<VoiceLineInfo> _record = _info => _positions.Add(VoiceOf(_info.Handle).transform.position);
            Audio.Voices.LineStarted += _record;
            ConversationHandle _handle = Audio.Voices.Play(MakeConversation(
                new ConversationLine { Voice = MakeVoice(_alice, 0.1f) },
                new ConversationLine { Voice = MakeVoice(_bob, 0.1f) },
                new ConversationLine { Voice = MakeVoice(_alice, 0.1f) }));
            yield return WaitFor(() => !_handle.IsPlaying, 3f);
            Audio.Voices.LineStarted -= _record;

            Assert.AreEqual(3, started.Count);
            Assert.AreSame(_aliceEmitter, started[0].Emitter);
            Assert.AreSame(_bobEmitter, started[1].Emitter);
            Assert.AreSame(_aliceEmitter, started[2].Emitter);
            Assert.AreEqual(new Vector3(-4f, 0f, 0f), _positions[0]);
            Assert.AreEqual(new Vector3(4f, 0f, 0f), _positions[1]);
            Assert.AreEqual(new Vector3(-4f, 0f, 0f), _positions[2]);
        }

        [UnityTest]
        public IEnumerator Emitter_DestroyedMidLine_ConversationContinues2D() {
            SpeakerAsset _speaker = MakeSpeaker("A");
            AudioEmitter _emitter = MakeEmitter(_speaker, new Vector3(3f, 2f, 1f));
            ConversationHandle _handle = Audio.Voices.Play(MakeConversation(
                new ConversationLine { Voice = MakeVoice(_speaker, 0.4f) },
                new ConversationLine { Voice = MakeVoice(_speaker, 0.4f) }));
            AudioHandle _first = started[0].Handle;
            yield return new WaitForSecondsRealtime(0.1f);

            Object.Destroy(_emitter.gameObject);
            yield return null;
            yield return null;
            Assert.IsTrue(_first.IsValid, "The line keeps playing after its emitter is destroyed.");
            Assert.AreEqual(new Vector3(3f, 2f, 1f), VoiceOf(_first).transform.position);

            yield return WaitFor(() => started.Count >= 2, 2f);
            Assert.AreEqual(2, started.Count);
            Assert.IsNull(started[1].Emitter);
            Assert.AreEqual(0f, VoiceOf(started[1].Handle).Source.spatialBlend);
            Assert.IsFalse(endedInterrupted[0]);
            yield return WaitFor(() => !_handle.IsPlaying, 2f);
            Assert.IsFalse(_handle.IsPlaying);
            Assert.AreEqual(2, ended.Count);
        }

        [UnityTest]
        public IEnumerator Emitter_DestroyedWithQueuedLines_PlaysThemSequentially2D() {
            SpeakerAsset _speaker = MakeSpeaker("A");
            AudioEmitter _emitter = MakeEmitter(_speaker, Vector3.one);
            _emitter.Say(MakeVoice(_speaker, 0.2f));
            _emitter.Say(MakeVoice(_speaker, 0.2f));
            _emitter.Say(MakeVoice(_speaker, 0.2f));
            Object.Destroy(_emitter.gameObject);

            yield return WaitFor(() => ended.Count >= 3, 3f);
            Assert.AreEqual(3, started.Count);
            Assert.IsNull(started[1].Emitter);
            Assert.IsNull(started[2].Emitter);
            Assert.GreaterOrEqual(startTimes[2], endTimes[1], "Queued lines of a destroyed emitter still play one at a time.");
        }

        [UnityTest]
        public IEnumerator StaleConversationHandle_IsNoOp() {
            SpeakerAsset _speaker = MakeSpeaker("A");
            ConversationHandle _old = Audio.Voices.Play(MakeConversation(new ConversationLine { Voice = MakeVoice(_speaker, 0.1f, false) }));
            yield return WaitFor(() => !_old.IsPlaying, 2f);
            ConversationHandle _new = Audio.Voices.Play(MakeConversation(new ConversationLine { Voice = MakeVoice(_speaker, 1f, false) }));
            _old.Stop();
            Assert.IsFalse(_old.IsPlaying);
            Assert.AreEqual(-1, _old.CurrentLine);
            Assert.IsTrue(_new.IsPlaying);
            Assert.AreEqual(1, ended.Count, "Only the old conversation line ended.");
        }

        [UnityTest]
        public IEnumerator LineEvents_ThrowingSubscriber_IsIsolated_AndSurviveRebuild() {
            Action<VoiceLineInfo> _throwStarted = _ => throw new InvalidOperationException("line started subscriber");
            Action<VoiceLineInfo, bool> _throwEnded = (_, _) => throw new InvalidOperationException("line ended subscriber");
            Audio.Voices.LineStarted += _throwStarted;
            Audio.Voices.LineEnded += _throwEnded;
            try {
                // Rebuilding the runtime keeps the subscriptions.
                Audio.InitializeForTests(settings);
                LogAssert.Expect(LogType.Exception, "InvalidOperationException: line started subscriber");
                LogAssert.Expect(LogType.Exception, "InvalidOperationException: line ended subscriber");
                AudioHandle _handle = Audio.Voices.Play(MakeVoice(MakeSpeaker("A"), 0.1f, false));
                Assert.IsTrue(_handle.IsValid);
                Assert.AreEqual(1, started.Count, "The other subscriber still receives LineStarted.");
                yield return WaitFor(() => ended.Count > 0, 2f);
                Assert.AreEqual(1, ended.Count);
            } finally {
                Audio.Voices.LineStarted -= _throwStarted;
                Audio.Voices.LineEnded -= _throwEnded;
            }
        }

        [UnityTest]
        public IEnumerator Conversation_PauseAfter_WaitsWhileVoiceBusPaused() {
            SpeakerAsset _speaker = MakeSpeaker("A");
            ConversationHandle _handle = Audio.Voices.Play(MakeConversation(
                new ConversationLine { Voice = MakeVoice(_speaker, 0.1f, false), PauseAfter = 0.3f },
                new ConversationLine { Voice = MakeVoice(_speaker, 0.1f, false) }));
            yield return WaitFor(() => ended.Count > 0, 2f);
            Audio.Pause(AudioBus.Voice);
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.AreEqual(1, started.Count, "PauseAfter must not count while the Voice bus is paused.");
            Audio.Resume(AudioBus.Voice);
            yield return WaitFor(() => started.Count > 1, 2f);
            Assert.AreEqual(2, started.Count);
            yield return WaitFor(() => !_handle.IsPlaying, 2f);
        }
    }
}
