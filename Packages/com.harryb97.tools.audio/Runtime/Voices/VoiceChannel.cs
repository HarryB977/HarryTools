using System;
using System.Collections.Generic;
using HarryTools.DomainReload;
using UnityEngine;

namespace HarryTools.Audio {
    /// <summary>
    /// Voice lines and conversations. A line is said by an AudioEmitter (the one given, else the active emitter of its speaker),
    /// follows it with the 3D settings of the VoiceAsset and gets its effect (emitter Effect, else the speaker DefaultEffect).
    /// Without emitter the line plays in 2D. An emitter says one line at a time: with Queue the next lines wait in order and
    /// their handles are valid at once (IsPlaying false until they start; Stop removes them); with Interrupt the current line
    /// fades out in 0.1 s and the new one starts.
    /// Queued lines hold a reserved voice from the pool: it is prepared (clip, emitter, effect) but not started until its turn.
    /// If the emitter is destroyed or disabled, its current line ends at the last position and its queued lines look for
    /// another active emitter of their speaker, else play in 2D, still one at a time.
    /// </summary>
    public sealed class VoiceChannel {
        private enum LineState : byte {
            /// <summary> Waiting for its emitter: the voice is reserved, not started. </summary>
            Queued,
            /// <summary> Started, still in the PlayDelayed delay. </summary>
            Started,
            /// <summary> Audible: LineStarted was raised. </summary>
            Sounding
        }

        private struct Line {
            public AudioHandle Handle;
            public VoiceAsset Voice;
            /// <summary> Emitter whose queue the line belongs to. Kept even after that emitter is destroyed so its lines stay sequential. Null: no queue. </summary>
            public AudioEmitter Key;
            /// <summary> Emitter the line follows, or null when it plays in 2D. </summary>
            public AudioEmitter Emitter;
            public LineState State;
            public float Duration;
        }

        // ==================== VARIABLES ===================
        /// <summary> Fade used when a line is interrupted by another line of an Interrupt emitter. </summary>
        internal const float InterruptFade = 0.1f;

        private readonly AudioRuntime runtime;
        private readonly List<Line> lines = new List<Line>(16);
        // Static so subscribers survive a runtime rebuild (the host destroyed from outside).
        [ClearOnReload] private static Action<VoiceLineInfo> lineStarted;
        [ClearOnReload] private static Action<VoiceLineInfo, bool> lineEnded;

        // ==================== CONSTRUCTOR ===================
        internal VoiceChannel(AudioRuntime _runtime) {
            runtime = _runtime;
            Conversations = new ConversationRunner(_runtime, this);
        }

        // ==================== EVENTS ===================
        /// <summary> Raised when a line becomes audible (after its Delay), not when it is queued. Subscriptions survive a rebuild of the audio runtime. </summary>
        public event Action<VoiceLineInfo> LineStarted {
            add => lineStarted += value;
            remove => lineStarted -= value;
        }

        /// <summary>
        /// Raised once for every line that raised LineStarted: when it finishes (interrupted = false), or when it is stopped, interrupted
        /// or starts fading out (interrupted = true). Subscriptions survive a rebuild of the audio runtime.
        /// </summary>
        public event Action<VoiceLineInfo, bool> LineEnded {
            add => lineEnded += value;
            remove => lineEnded -= value;
        }

        // ==================== PROPERTIES ===================
        internal ConversationRunner Conversations { get; }

        // ==================== PUBLIC ===================
        /// <summary> Says the line from the catalog. See Play(VoiceAsset, AudioEmitter). </summary>
        public AudioHandle Play(VoiceRef _voice, AudioEmitter _emitter = null) {
            if (_voice.TryResolve(out VoiceAsset _asset)) return Play(_asset, _emitter);
            if (_voice.IsValid) HarryTools.LoggerTool.Logger.LogWarning("Audio", "Voice id " + _voice.Id + " is not in the audio catalog.");
            return AudioHandle.Invalid;
        }

        /// <summary>
        /// Says the line through the emitter. Null (or a disabled emitter) uses the active emitter of the voice speaker, else plays in 2D.
        /// When the emitter is speaking, the line is queued or interrupts the current one, as its BusyPolicy says.
        /// </summary>
        public AudioHandle Play(VoiceAsset _voice, AudioEmitter _emitter = null) {
            if (_voice == null) return AudioHandle.Invalid;
            if (!IsUsable(_emitter)) {
                _emitter = null;
                if (_voice.Speaker != null) AudioEmitter.TryGetActive(_voice.Speaker, out _emitter);
            }
            if (_emitter == null) return StartLine(_voice, null, false);

            int _current = FindCurrent(_emitter);
            if (_current >= 0 || FindFirstQueued(_emitter) >= 0) {
                if (_emitter.BusyPolicy == EmitterBusyPolicy.Queue) return StartLine(_voice, _emitter, true);
                // Interrupt: the queued lines keep waiting behind the new one.
                if (_current >= 0) EndLine(_current, true, InterruptFade, false);
            }
            return StartLine(_voice, _emitter, false);
        }

        /// <summary> Plays the conversation from the catalog. See Play(ConversationAsset). </summary>
        public ConversationHandle Play(ConversationRef _conversation) {
            if (_conversation.TryResolve(out ConversationAsset _asset)) return Play(_asset);
            if (_conversation.IsValid) HarryTools.LoggerTool.Logger.LogWarning("Audio", "Conversation id " + _conversation.Id + " is not in the audio catalog.");
            return default;
        }

        /// <summary>
        /// Plays the lines in order. Each one is said by the active emitter of its speaker at that moment (else 2D) and is followed by its
        /// PauseAfter, in unscaled seconds that do not count while Audio (or the bus of the line) is paused. The first line starts at once.
        /// </summary>
        public ConversationHandle Play(ConversationAsset _conversation) => Conversations.Start(_conversation);

        /// <summary> Stops every conversation and every voice line, queued ones included. </summary>
        public void StopAll(float _fade = 0.1f) {
            Conversations.StopAll(_fade);
            // Queued lines first, so stopping a current line does not start the next one.
            for (int _i = lines.Count - 1; _i >= 0; _i--) {
                if (_i >= lines.Count || lines[_i].State != LineState.Queued) continue;
                EndLine(_i, true, 0f, false);
            }
            for (int _i = lines.Count - 1; _i >= 0; _i--) {
                if (_i >= lines.Count) continue;
                EndLine(_i, true, _fade, false);
            }
        }

        // ==================== INTERNAL ===================
        /// <summary> True while the emitter has a line started (in its delay or audible). Queued lines do not count. </summary>
        internal bool IsSpeaking(AudioEmitter _emitter) => !ReferenceEquals(_emitter, null) && FindCurrent(_emitter) >= 0;

        /// <summary> Stops a line with the fade, raising LineEnded (interrupted) if it was audible, and moves its emitter queue on. </summary>
        internal void StopLine(AudioHandle _handle, float _fade) {
            int _index = Find(_handle);
            if (_index >= 0) {
                EndLine(_index, true, _fade, true);
                return;
            }
            if (runtime.Voices.TryGetVoice(_handle, out AudioVoice _voice)) runtime.Voices.Stop(_voice, _fade);
        }

        /// <summary> Called by VoiceManager after it released a voice. _natural: it played to its end. </summary>
        internal void OnVoiceReleased(AudioHandle _handle, bool _natural) {
            int _index = Find(_handle);
            if (_index >= 0) EndLine(_index, !_natural, -1f, true);
        }

        /// <summary> Raises LineStarted for lines whose delay ended, and ends the lines that started fading out (stopped through their handle). </summary>
        internal void Tick() {
            VoiceManager _voices = runtime.Voices;
            for (int _i = 0; _i < lines.Count; _i++) {
                Line _line = lines[_i];
                if (_line.State == LineState.Queued) continue;
                if (!_voices.TryGetVoice(_line.Handle, out AudioVoice _voice)) {
                    // Released without notice (should not happen): end it as finished.
                    EndLine(_i, false, -1f, true);
                    _i = -1;
                    continue;
                }
                if (_voice.IsFadingOut) {
                    EndLine(_i, true, -1f, true);
                    _i = -1;
                    continue;
                }
                if (_line.State == LineState.Started && !_voice.InDelay) {
                    MarkSounding(_i);
                    _i = -1;
                }
            }
        }

        internal void Dispose() {
            Conversations.Dispose();
            lines.Clear();
        }

        // ==================== PRIVATE ===================
        private static bool IsUsable(AudioEmitter _emitter) => _emitter != null && _emitter.isActiveAndEnabled;

        private static AudioEffectPreset EffectOf(VoiceAsset _voice, AudioEmitter _emitter) {
            if (_emitter != null && _emitter.Effect != null) return _emitter.Effect;
            SpeakerAsset _speaker = _voice.Speaker != null ? _voice.Speaker : _emitter != null ? _emitter.Speaker : null;
            return _speaker != null ? _speaker.DefaultEffect : null;
        }

        // Takes a voice for the line: started now, or reserved (queued) behind the current line of its emitter.
        private AudioHandle StartLine(VoiceAsset _voice, AudioEmitter _emitter, bool _queue) {
            Transform _follow = _emitter != null ? _emitter.transform : null;
            var _request = new PlaybackRequest(_voice, Vector3.zero, _follow, _emitter, EffectOf(_voice, _emitter), _emitter == null, _queue);
            AudioHandle _handle = runtime.Voices.Start(_request);
            if (!runtime.Voices.TryGetVoice(_handle, out AudioVoice _source)) return AudioHandle.Invalid;

            lines.Add(new Line {
                Handle = _handle,
                Voice = _voice,
                Key = _emitter,
                Emitter = _emitter,
                State = _queue ? LineState.Queued : LineState.Started,
                Duration = _source.ClipLength / Mathf.Max(0.01f, Mathf.Abs(_source.Pitch))
            });
            if (!_queue && !_source.InDelay) MarkSounding(lines.Count - 1);
            return _handle;
        }

        // Removes the line. _fade >= 0 also stops its voice; LineEnded is raised if it was audible; _advance starts the next queued line of its emitter.
        private void EndLine(int _index, bool _interrupted, float _fade, bool _advance) {
            Line _line = lines[_index];
            lines.RemoveAt(_index);
            if (_fade >= 0f && runtime.Voices.TryGetVoice(_line.Handle, out AudioVoice _voice)) runtime.Voices.Stop(_voice, _fade);
            if (_line.State == LineState.Sounding) RaiseEnded(Info(_line), _interrupted);
            if (_advance && _line.State != LineState.Queued && !ReferenceEquals(_line.Key, null)) Advance(_line.Key);
        }

        private void MarkSounding(int _index) {
            Line _line = lines[_index];
            _line.State = LineState.Sounding;
            lines[_index] = _line;
            RaiseStarted(Info(_line));
        }

        // Starts queued lines of the emitter until one of them is current. Lines whose emitter is gone move to another active
        // emitter of their speaker (queued there if it is busy) or play in 2D under the same queue.
        private void Advance(AudioEmitter _key) {
            VoiceManager _voices = runtime.Voices;
            while (FindCurrent(_key) < 0) {
                int _index = FindFirstQueued(_key);
                if (_index < 0) return;
                Line _line = lines[_index];
                if (!_voices.TryGetVoice(_line.Handle, out AudioVoice _voice)) {
                    lines.RemoveAt(_index);
                    continue;
                }

                if (!IsUsable(_line.Emitter)) {
                    AudioEmitter _other = null;
                    SpeakerAsset _speaker = _line.Voice.Speaker;
                    if (_speaker != null && AudioEmitter.TryGetActive(_speaker, out _other) && !ReferenceEquals(_other, _key)) {
                        _line.Key = _other;
                        _line.Emitter = _other;
                        _voice.SetFollow(_other.transform);
                        _line.Voice.GetSpatial().ApplyTo(_voice.Source);
                        EffectApplier.Apply(_voice, EffectOf(_line.Voice, _other), runtime.GetOutput(_line.Voice));
                        lines[_index] = _line;
                        // Busy: it now waits in the queue of the other emitter.
                        if (FindCurrent(_other) >= 0) continue;
                    } else {
                        _line.Emitter = null;
                        _voice.SetFollow(null);
                        _voice.Source.spatialBlend = 0f;
                    }
                }

                _line.State = LineState.Started;
                lines[_index] = _line;
                _voices.Activate(_voice);
                if (!_voice.InDelay) MarkSounding(_index);
            }
        }

        private VoiceLineInfo Info(in Line _line) {
            AudioEmitter _emitter = _line.Emitter != null ? _line.Emitter : null;
            return new VoiceLineInfo(_line.Voice, _line.Voice != null ? _line.Voice.Speaker : null, _emitter, _line.Duration, _line.Handle);
        }

        private int Find(AudioHandle _handle) {
            for (int _i = 0; _i < lines.Count; _i++) {
                if (lines[_i].Handle == _handle) return _i;
            }
            return -1;
        }

        private int FindCurrent(AudioEmitter _key) {
            for (int _i = 0; _i < lines.Count; _i++) {
                if (ReferenceEquals(lines[_i].Key, _key) && lines[_i].State != LineState.Queued) return _i;
            }
            return -1;
        }

        private int FindFirstQueued(AudioEmitter _key) {
            for (int _i = 0; _i < lines.Count; _i++) {
                if (ReferenceEquals(lines[_i].Key, _key) && lines[_i].State == LineState.Queued) return _i;
            }
            return -1;
        }

        // A throwing subscriber must not break the voice bookkeeping.
        private static void RaiseStarted(VoiceLineInfo _info) {
            if (lineStarted == null) return;
            try { lineStarted(_info); } catch (Exception _exception) { Debug.LogException(_exception); }
        }

        private static void RaiseEnded(VoiceLineInfo _info, bool _interrupted) {
            if (lineEnded == null) return;
            try { lineEnded(_info, _interrupted); } catch (Exception _exception) { Debug.LogException(_exception); }
        }
    }
}
