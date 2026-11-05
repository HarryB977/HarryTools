using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace HarryTools.Audio {
    /// <summary>
    /// Reference to one playback. It stores the voice index and the voice generation, so a handle whose sound already ended
    /// (and whose voice now plays another sound) is stale: every operation on it does nothing.
    /// </summary>
    public readonly struct AudioHandle : IEquatable<AudioHandle> {
        // ==================== VARIABLES ===================
        /// <summary> Handle that never refers to a playback. Same as default. </summary>
        public static readonly AudioHandle Invalid = default;

        private readonly int index;
        private readonly int generation;

        // ==================== CONSTRUCTORS ===================
        internal AudioHandle(int _index, int _generation) {
            index = _index;
            generation = _generation;
        }

        // ==================== PROPERTIES ===================
        internal int VoiceIndex => index;
        internal int Generation => generation;

        /// <summary> True while the playback is alive: playing, delayed, paused or fading out. </summary>
        public bool IsValid => TryGetVoice(out _);
        public bool IsPlaying => TryGetVoice(out AudioVoice _voice) && !_voice.UserPaused && _voice.Source.isPlaying;
        public bool IsPaused => TryGetVoice(out AudioVoice _voice) && _voice.UserPaused;
        /// <summary> Asset being played, or null when the handle is no longer valid. </summary>
        public AudioAsset Asset => TryGetVoice(out AudioVoice _voice) ? _voice.Asset : null;

        // ==================== PUBLIC ===================
        /// <summary> Moves the sound to a world position and stops following its target. </summary>
        public AudioHandle At(Vector3 _position) {
            if (TryGetVoice(out AudioVoice _voice)) _voice.SetPosition(_position);
            return this;
        }

        /// <summary> Makes the sound follow a transform. If the transform is destroyed the sound stays at its last position. </summary>
        public AudioHandle Follow(Transform _target) {
            if (TryGetVoice(out AudioVoice _voice)) _voice.SetFollow(_target);
            return this;
        }

        /// <summary> Rolls a new base volume in the range, replacing the asset range. </summary>
        public AudioHandle WithVolume(float _min, float _max) {
            if (TryGetVoice(out AudioVoice _voice)) Audio.Current.Voices.SetBaseVolume(_voice, UnityEngine.Random.Range(_min, _max));
            return this;
        }

        /// <summary> Rolls a new pitch in the range, replacing the asset range. </summary>
        public AudioHandle WithPitch(float _min, float _max) {
            if (TryGetVoice(out AudioVoice _voice)) _voice.SetPitch(UnityEngine.Random.Range(_min, _max));
            return this;
        }

        /// <summary> Volume multiplier of this playback (0..1), reached over the duration in unscaled seconds. </summary>
        public AudioHandle SetVolume(float _linear, float _duration = 0f) {
            if (TryGetVoice(out AudioVoice _voice)) Audio.Current.Voices.SetHandleVolume(_voice, _linear, _duration);
            return this;
        }

        /// <summary> Stops the sound. A negative fade uses the asset FadeOut; 0 stops at once. </summary>
        public void Stop(float _fade = -1f) {
            if (TryGetVoice(out AudioVoice _voice)) Audio.Current.Voices.Stop(_voice, _fade);
        }

        public void Pause() {
            if (TryGetVoice(out AudioVoice _voice)) _voice.Pause();
        }

        public void Resume() {
            if (TryGetVoice(out AudioVoice _voice)) _voice.Resume();
        }

        /// <summary>
        /// Calls back once when this playback ends, whatever the reason (finished, stopped or stolen).
        /// Each playback keeps one callback: a second call replaces the first. Not called when the audio runtime shuts down.
        /// </summary>
        public AudioHandle OnEnded(Action<AudioHandle> _callback) {
            if (TryGetVoice(out AudioVoice _voice)) _voice.EndedCallback = _callback;
            return this;
        }

        /// <summary> Completes when the playback ends (or at once if the handle is not valid). Throws OperationCanceledException when cancelled. </summary>
        public UniTask WaitForEnd(CancellationToken _token = default) {
            if (!IsValid) return _token.IsCancellationRequested ? UniTask.FromCanceled(_token) : UniTask.CompletedTask;
            AudioHandle _self = this;
            return UniTask.WaitUntil(() => !_self.IsValid, PlayerLoopTiming.Update, _token);
        }

        public bool Equals(AudioHandle _other) => index == _other.index && generation == _other.generation;
        public override bool Equals(object _obj) => _obj is AudioHandle _other && Equals(_other);
        public override int GetHashCode() => (index * 397) ^ generation;
        public override string ToString() => generation == 0 ? "AudioHandle(Invalid)" : $"AudioHandle({index}:{generation})";

        public static bool operator ==(AudioHandle _a, AudioHandle _b) => _a.Equals(_b);
        public static bool operator !=(AudioHandle _a, AudioHandle _b) => !_a.Equals(_b);

        // ==================== PRIVATE ===================
        private bool TryGetVoice(out AudioVoice _voice) {
            AudioRuntime _runtime = Audio.Current;
            if (generation == 0 || _runtime == null) {
                _voice = null;
                return false;
            }
            return _runtime.Voices.TryGetVoice(this, out _voice);
        }
    }
}
