using System;
using System.Collections.Generic;
using HarryUtils.ObjectPool;
using UnityEngine;

namespace HarryTools.Audio {
    /// <summary>
    /// Owns the pool of sound voices: starts playbacks (cooldown, instance limits, stealing), ticks them and releases them.
    /// Music voices are not part of this pool.
    /// </summary>
    internal sealed class VoiceManager {
        // Runtime state of an asset. Lives here, never on the ScriptableObject.
        private sealed class AssetState {
            public ClipSelector Selector;
            public float LastPlayTime;
            public bool HasPlayed;
            public int ActiveCount;
        }

        // A voice and the generation it had when collected: a callback may release and reuse it before we get to it.
        private readonly struct Pending {
            public readonly AudioVoice Voice;
            public readonly int Generation;

            public Pending(AudioVoice _voice) {
                Voice = _voice;
                Generation = _voice.Generation;
            }
        }

        // ==================== VARIABLES ===================
        // Not reset on reload on purpose: generations keep growing across runtimes, so a handle from a previous runtime never matches a new voice.
        private static int generationSeed;

        private readonly AudioRuntime runtime;
        private readonly Pool<AudioVoice> pool;
        private readonly List<AudioVoice> voices = new List<AudioVoice>();
        private readonly List<AudioVoice> active = new List<AudioVoice>();
        private readonly List<Pending> scratch = new List<Pending>();
        private readonly Dictionary<AudioAsset, AssetState> states = new Dictionary<AudioAsset, AssetState>();
        private readonly System.Random rng = new System.Random();
        private int scratchDepth;

        // ==================== CONSTRUCTOR ===================
        public VoiceManager(AudioRuntime _runtime, AudioVoice _template, Transform _parent, int _maxVoices, int _prewarm) {
            runtime = _runtime;
            pool = new Pool<AudioVoice>(_template, 0, Mathf.Max(1, _maxVoices), PoolOverflow.Fail, _parent);
            pool.Created += OnVoiceCreated;
            pool.Prewarm(Mathf.Min(_prewarm, _maxVoices));
        }

        // ==================== PROPERTIES ===================
        /// <summary> Voices currently in use, oldest first. </summary>
        public IReadOnlyList<AudioVoice> ActiveVoices => active;

        // ==================== PUBLIC ===================
        public AudioHandle Start(in PlaybackRequest _request) {
            AudioAsset _asset = _request.Asset;
            if (_asset == null) return AudioHandle.Invalid;
            AssetState _state = GetState(_asset);
            if (!_state.Selector.HasClips) return AudioHandle.Invalid;

            float _now = Time.unscaledTime;
            if (_asset.Cooldown > 0f && _state.HasPlayed && _now - _state.LastPlayTime < _asset.Cooldown) return AudioHandle.Invalid;

            if (_asset.MaxInstances > 0 && _state.ActiveCount >= _asset.MaxInstances) {
                if (_asset.LimitPolicy == InstanceLimitPolicy.Ignore) return AudioHandle.Invalid;
                AudioVoice _victim = _asset.LimitPolicy == InstanceLimitPolicy.StealQuietest ? FindQuietest(_asset) : FindOldest(_asset);
                if (_victim != null) Release(_victim);
            }

            Vector3 _position = _request.StartPosition;
            Vector2 _volumeRange = _asset.VolumeRange;
            float _baseVolume = UnityEngine.Random.Range(_volumeRange.x, _volumeRange.y);

            AudioVoice _voice = pool.Get();
            if (_voice == null) {
                float _score = _asset.Priority / 100f * _baseVolume * runtime.Mixer.GetSourceMultiplier(_asset.Bus) * Attenuation(_asset.GetSpatial(), _position);
                AudioVoice _victim = FindLowestScore(out float _lowest);
                if (_victim == null || _lowest >= _score) return AudioHandle.Invalid;
                Release(_victim);
                _voice = pool.Get();
                if (_voice == null) return AudioHandle.Invalid;
            }

            AudioClip _clip = _state.Selector.Next();
            if (_clip == null) {
                pool.Release(_voice);
                return AudioHandle.Invalid;
            }
            Vector2 _pitchRange = _asset.PitchRange;
            _voice.Begin(_asset, _clip, _position, _request.Follow, _baseVolume, UnityEngine.Random.Range(_pitchRange.x, _pitchRange.y), runtime.GetOutput(_asset));
            _voice.UpdateVolume(runtime.Mixer.GetSourceMultiplier(_voice.Bus), runtime.MultiplyOnSources);
            _voice.StartSource();

            active.Add(_voice);
            _state.ActiveCount++;
            _state.HasPlayed = true;
            _state.LastPlayTime = _now;

            var _handle = new AudioHandle(_voice.Index, _voice.Generation);
            runtime.RaiseStarted(_handle);
            return _handle;
        }

        /// <summary> The voice of the handle. False when the handle is invalid or stale (its generation does not match). </summary>
        public bool TryGetVoice(AudioHandle _handle, out AudioVoice _voice) {
            int _index = _handle.VoiceIndex;
            if ((uint)_index < (uint)voices.Count) {
                _voice = voices[_index];
                if (_voice != null && _voice.IsActive && _voice.Generation == _handle.Generation) return true;
            }
            _voice = null;
            return false;
        }

        public void Tick(float _unscaledDelta) {
            bool _onSource = runtime.MultiplyOnSources;
            BusMixer _mixer = runtime.Mixer;
            List<Pending> _finished = RentScratch();
            try {
                for (int _i = 0; _i < active.Count; _i++) {
                    AudioVoice _voice = active[_i];
                    if (_voice == null || _voice.Tick(_unscaledDelta, _mixer.GetSourceMultiplier(_voice.Bus), _onSource)) _finished.Add(new Pending(_voice));
                }
                for (int _i = 0; _i < _finished.Count; _i++) {
                    if (_finished[_i].Voice.Generation == _finished[_i].Generation) Release(_finished[_i].Voice);
                }
            } finally {
                ReturnScratch(_finished);
            }
        }

        /// <summary> Stops one voice. A negative fade uses the asset FadeOut; 0 (or a paused voice) releases it at once. </summary>
        public void Stop(AudioVoice _voice, float _fade) {
            if (_voice == null || !_voice.IsActive) return;
            if (_fade < 0f) _fade = _voice.Asset != null ? _voice.Asset.FadeOut : 0f;
            if (_fade <= 0f || _voice.UserPaused) Release(_voice);
            else _voice.BeginFadeOut(_fade);
        }

        /// <summary> Stops every voice of the asset, or every voice when the asset is null. </summary>
        public void StopAll(AudioAsset _asset, float _fade) {
            List<Pending> _targets = RentScratch();
            try {
                for (int _i = 0; _i < active.Count; _i++) {
                    if (ReferenceEquals(_asset, null) || ReferenceEquals(active[_i].Asset, _asset)) _targets.Add(new Pending(active[_i]));
                }
                for (int _i = 0; _i < _targets.Count; _i++) {
                    if (_targets[_i].Voice.Generation == _targets[_i].Generation) Stop(_targets[_i].Voice, _fade);
                }
            } finally {
                ReturnScratch(_targets);
            }
        }

        public void SetHandleVolume(AudioVoice _voice, float _linear, float _duration) {
            _voice.SetHandleVolume(_linear, _duration);
            _voice.UpdateVolume(runtime.Mixer.GetSourceMultiplier(_voice.Bus), runtime.MultiplyOnSources);
        }

        public void SetBaseVolume(AudioVoice _voice, float _volume) {
            _voice.SetBaseVolume(_volume);
            _voice.UpdateVolume(runtime.Mixer.GetSourceMultiplier(_voice.Bus), runtime.MultiplyOnSources);
        }

        /// <summary> Destroys every voice without raising Ended. Used when the runtime shuts down. </summary>
        public void Dispose() {
            pool.Created -= OnVoiceCreated;
            pool.Dispose();
            voices.Clear();
            active.Clear();
            states.Clear();
        }

        // ==================== PRIVATE ===================
        private void OnVoiceCreated(AudioVoice _voice) {
            _voice.Setup(voices.Count, ++generationSeed);
            _voice.name = "Voice " + voices.Count;
            voices.Add(_voice);
        }

        private AssetState GetState(AudioAsset _asset) {
            if (!states.TryGetValue(_asset, out AssetState _state)) {
                _state = new AssetState { Selector = new ClipSelector(_asset, rng) };
                states.Add(_asset, _state);
            }
            return _state;
        }

        private void Release(AudioVoice _voice) {
            if (ReferenceEquals(_voice, null) || !_voice.IsActive) return;
            var _handle = new AudioHandle(_voice.Index, _voice.Generation);
            Action<AudioHandle> _callback = _voice.EndedCallback;

            active.Remove(_voice);
            AudioAsset _asset = _voice.Asset;
            if (!ReferenceEquals(_asset, null) && states.TryGetValue(_asset, out AssetState _state)) _state.ActiveCount--;
            // A voice destroyed from outside is dropped by the pool instead of being returned.
            pool.Release(_voice);
            _voice.Invalidate(++generationSeed);

            if (_callback != null) {
                try { _callback(_handle); } catch (Exception _exception) { Debug.LogException(_exception); }
            }
            runtime.RaiseEnded(_handle);
        }

        private AudioVoice FindOldest(AudioAsset _asset) {
            for (int _i = 0; _i < active.Count; _i++) {
                if (ReferenceEquals(active[_i].Asset, _asset)) return active[_i];
            }
            return null;
        }

        private AudioVoice FindQuietest(AudioAsset _asset) {
            AudioVoice _found = null;
            float _lowest = float.MaxValue;
            for (int _i = 0; _i < active.Count; _i++) {
                AudioVoice _voice = active[_i];
                if (!ReferenceEquals(_voice.Asset, _asset) || _voice.EffectiveVolume >= _lowest) continue;
                _lowest = _voice.EffectiveVolume;
                _found = _voice;
            }
            return _found;
        }

        // Audibility score: Priority/100 × effective volume × estimated distance attenuation.
        private AudioVoice FindLowestScore(out float _lowest) {
            AudioVoice _found = null;
            _lowest = float.MaxValue;
            for (int _i = 0; _i < active.Count; _i++) {
                AudioVoice _voice = active[_i];
                if (_voice == null || _voice.Asset == null) continue;
                float _score = _voice.Priority / 100f * _voice.EffectiveVolume * Attenuation(_voice.Asset.GetSpatial(), _voice.transform.position);
                if (_score >= _lowest) continue;
                _lowest = _score;
                _found = _voice;
            }
            return _found;
        }

        /// <summary> Gain of the spatial settings at the distance to the listener, estimated from the rolloff curve. 1 for 2D or without listener. </summary>
        private float Attenuation(in SpatialSettings _spatial, Vector3 _position) {
            if (_spatial.SpatialBlend <= 0f) return 1f;
            Transform _listener = runtime.ListenerTracker.Listener;
            if (_listener == null) return 1f;

            float _min = Mathf.Max(0.0001f, _spatial.MinDistance);
            float _max = Mathf.Max(_min, _spatial.MaxDistance);
            float _distance = Mathf.Min(Vector3.Distance(_listener.position, _position), _max);
            float _gain;
            if (_spatial.Rolloff == AudioRolloffMode.Linear) {
                _gain = _max > _min ? 1f - Mathf.Clamp01((_distance - _min) / (_max - _min)) : 1f;
            } else if (_spatial.Rolloff == AudioRolloffMode.Custom && _spatial.HasCustomCurve) {
                _gain = Mathf.Clamp01(_spatial.CustomRolloff.Evaluate(_distance / _max));
            } else {
                _gain = _distance <= _min ? 1f : _min / _distance;
            }
            return Mathf.Lerp(1f, _gain, _spatial.SpatialBlend);
        }

        // A nested Tick/StopAll (from an Ended callback) gets its own list so the outer iteration is not disturbed.
        private List<Pending> RentScratch() => scratchDepth++ == 0 ? scratch : new List<Pending>();

        private void ReturnScratch(List<Pending> _list) {
            _list.Clear();
            scratchDepth--;
        }
    }
}
