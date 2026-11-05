using System;
using HarryUtils.ObjectPool;
using UnityEngine;
using UnityEngine.Audio;

namespace HarryTools.Audio {
    /// <summary>
    /// A pooled AudioSource with its filters (disabled until an effect uses them) and the state of the current playback.
    /// Driven by VoiceManager; it has no Update of its own.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class AudioVoice : MonoBehaviour, IPoolable {
        // ==================== VARIABLES ===================
        #region Components
        private AudioSource source;
        private AudioLowPassFilter lowPass;
        private AudioHighPassFilter highPass;
        private AudioDistortionFilter distortion;
        private AudioEchoFilter echo;
        #endregion

        #region Playback state
        private Transform followTarget;
        private bool following;
        private float clipLength;
        private float delay;
        private float elapsed;
        private int notPlayingTicks;

        private float fadeInDuration;
        private float fadeInElapsed;
        private bool fadingOut;
        private float fadeOutDuration;
        private float fadeOutRemaining;

        private float handleVolume = 1f;
        private float handleFrom = 1f;
        private float handleTo = 1f;
        private float handleDuration;
        private float handleElapsed;
        #endregion

        // ==================== PROPERTIES ===================
        public int Index { get; private set; }
        public int Generation { get; private set; }
        public AudioSource Source => source;
        public AudioLowPassFilter LowPass => lowPass;
        public AudioHighPassFilter HighPass => highPass;
        public AudioDistortionFilter Distortion => distortion;
        public AudioEchoFilter Echo => echo;

        /// <summary> True between Begin and the release back to the pool. </summary>
        public bool IsActive { get; private set; }
        public AudioAsset Asset { get; private set; }
        public AudioBus Bus { get; private set; }
        public int Priority { get; private set; }
        public float BaseVolume { get; private set; }
        public float Pitch { get; private set; }
        public bool UserPaused { get; private set; }
        public bool IsFadingOut => fadingOut;
        /// <summary> Current volume including fades, the handle multiplier and the bus multiplier. </summary>
        public float EffectiveVolume { get; private set; }
        /// <summary> Level the voice is heading to: base × handle volume × fade-out × bus, ignoring fade-in. Used to pick voices to steal, so a voice that is still fading in is not treated as silent. </summary>
        public float TargetVolume { get; private set; }
        public Action<AudioHandle> EndedCallback { get; set; }

        // ==================== SETUP ===================
        /// <summary> Caches the components. Called once when the pool creates the instance. </summary>
        public void Setup(int _index, int _generation) {
            Index = _index;
            Generation = _generation;
            source = GetComponent<AudioSource>();
            lowPass = GetComponent<AudioLowPassFilter>();
            highPass = GetComponent<AudioHighPassFilter>();
            distortion = GetComponent<AudioDistortionFilter>();
            echo = GetComponent<AudioEchoFilter>();
        }

        /// <summary> Adds the AudioSource, the filters (disabled) and the voice to a template object. </summary>
        public static AudioVoice CreateTemplate(Transform _parent) {
            var _go = new GameObject("Voice");
            _go.SetActive(false);
            _go.transform.SetParent(_parent, false);
            var _source = _go.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _go.AddComponent<AudioLowPassFilter>().enabled = false;
            _go.AddComponent<AudioHighPassFilter>().enabled = false;
            _go.AddComponent<AudioDistortionFilter>().enabled = false;
            _go.AddComponent<AudioEchoFilter>().enabled = false;
            return _go.AddComponent<AudioVoice>();
        }

        // ==================== PLAYBACK ===================
        public void Begin(AudioAsset _asset, AudioClip _clip, Vector3 _position, Transform _follow, float _baseVolume, float _pitch, AudioMixerGroup _output) {
            IsActive = true;
            Asset = _asset;
            Bus = _asset.Bus;
            Priority = _asset.Priority;
            BaseVolume = _baseVolume;
            UserPaused = false;

            followTarget = _follow;
            following = _follow != null;
            transform.position = _position;

            clipLength = _clip.length;
            delay = _asset.Delay;
            elapsed = 0f;
            notPlayingTicks = 0;
            fadeInDuration = _asset.FadeIn;
            fadeInElapsed = 0f;
            fadingOut = false;
            fadeOutDuration = 0f;
            fadeOutRemaining = 0f;
            handleVolume = handleFrom = handleTo = 1f;
            handleDuration = handleElapsed = 0f;

            source.clip = _clip;
            source.loop = _asset.Loop;
            source.priority = Mathf.Clamp(256 - _asset.Priority * 256 / 100, 0, 256);
            source.outputAudioMixerGroup = _output;
            _asset.GetSpatial().ApplyTo(source);
            SetPitch(_pitch);
        }

        public void StartSource() {
            if (delay > 0f) source.PlayDelayed(delay);
            else source.Play();
        }

        /// <summary> Advances follow, fades and end detection. Returns true when the voice has finished and must be released. </summary>
        public bool Tick(float _delta, float _busMultiplier, bool _multiplyOnSource) {
            if (following) {
                if (followTarget != null) transform.position = followTarget.position;
                else {
                    // Target destroyed: keep the last position.
                    following = false;
                    followTarget = null;
                }
            }

            if (UserPaused) {
                UpdateVolume(_busMultiplier, _multiplyOnSource);
                return false;
            }

            elapsed += _delta;
            if (fadeInElapsed < fadeInDuration) fadeInElapsed += _delta;
            if (handleElapsed < handleDuration) {
                handleElapsed += _delta;
                handleVolume = Mathf.Lerp(handleFrom, handleTo, handleElapsed / handleDuration);
            }
            if (fadingOut) {
                fadeOutRemaining -= _delta;
                if (fadeOutRemaining <= 0f) return true;
            }
            UpdateVolume(_busMultiplier, _multiplyOnSource);

            if (source.isPlaying) {
                notPlayingTicks = 0;
                return false;
            }
            // Not playing: done once the estimated duration passed, or after several ticks in a row (pitch changes, focus loss).
            notPlayingTicks++;
            return (!source.loop && elapsed >= EstimatedDuration) || notPlayingTicks >= 4;
        }

        public void UpdateVolume(float _busMultiplier, bool _multiplyOnSource) {
            float _fadeIn = fadeInDuration > 0f ? Mathf.Clamp01(fadeInElapsed / fadeInDuration) : 1f;
            float _fadeOut = fadingOut ? Mathf.Clamp01(fadeOutRemaining / fadeOutDuration) : 1f;
            float _target = BaseVolume * handleVolume * _fadeOut;
            float _local = _target * _fadeIn;
            TargetVolume = _target * _busMultiplier;
            EffectiveVolume = _local * _busMultiplier;
            source.volume = _multiplyOnSource ? EffectiveVolume : _local;
        }

        /// <summary> Fades to silence over the duration, continuing from the current fade-out level when one is running. </summary>
        public void BeginFadeOut(float _duration) {
            float _level = fadingOut ? Mathf.Clamp01(fadeOutRemaining / fadeOutDuration) : 1f;
            if (fadingOut && fadeOutRemaining <= _duration) return;
            fadingOut = true;
            fadeOutRemaining = _duration * _level;
            fadeOutDuration = _duration;
        }

        public void SetHandleVolume(float _linear, float _duration) {
            _linear = Mathf.Max(0f, _linear);
            if (_duration <= 0f) {
                handleVolume = handleFrom = handleTo = _linear;
                handleDuration = handleElapsed = 0f;
                return;
            }
            handleFrom = handleVolume;
            handleTo = _linear;
            handleDuration = _duration;
            handleElapsed = 0f;
        }

        public void SetBaseVolume(float _volume) => BaseVolume = Mathf.Max(0f, _volume);

        public void SetPitch(float _pitch) {
            Pitch = _pitch;
            source.pitch = _pitch;
        }

        public void SetPosition(Vector3 _position) {
            following = false;
            followTarget = null;
            transform.position = _position;
        }

        public void SetFollow(Transform _target) {
            followTarget = _target;
            following = _target != null;
            if (following) transform.position = _target.position;
        }

        public void Pause() {
            if (UserPaused) return;
            UserPaused = true;
            source.Pause();
        }

        public void Resume() {
            if (!UserPaused) return;
            UserPaused = false;
            notPlayingTicks = 0;
            source.UnPause();
        }

        /// <summary> Bumps the generation so every handle to the previous playback becomes stale. </summary>
        public void Invalidate(int _generation) => Generation = _generation;

        // ==================== POOL ===================
        void IPoolable.OnGetFromPool() { }

        void IPoolable.OnReleaseToPool() {
            IsActive = false;
            ResetComponents();
            Asset = null;
            followTarget = null;
            following = false;
            EndedCallback = null;
            UserPaused = false;
            fadingOut = false;
            EffectiveVolume = 0f;
            TargetVolume = 0f;
        }

        // ==================== PRIVATE ===================
        // Back to a clean AudioSource and disabled filters, whatever the last playback (or an effect) changed.
        private void ResetComponents() {
            if (source != null) {
                source.Stop();
                source.clip = null;
                source.outputAudioMixerGroup = null;
                source.loop = false;
                source.pitch = 1f;
                source.volume = 1f;
                source.mute = false;
                source.panStereo = 0f;
                source.reverbZoneMix = 1f;
                source.priority = 128;
                source.ignoreListenerPause = false;
                source.ignoreListenerVolume = false;
                source.bypassEffects = false;
                source.bypassListenerEffects = false;
                source.bypassReverbZones = false;
                source.spatialBlend = 0f;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.minDistance = 1f;
                source.maxDistance = 500f;
                source.spread = 0f;
                source.dopplerLevel = 1f;
            }
            if (lowPass != null) lowPass.enabled = false;
            if (highPass != null) highPass.enabled = false;
            if (distortion != null) distortion.enabled = false;
            if (echo != null) echo.enabled = false;
        }

        // Delay plus the clip length at the current pitch, measured from Begin.
        private float EstimatedDuration => delay + clipLength / Mathf.Max(0.01f, Mathf.Abs(Pitch));
    }
}
