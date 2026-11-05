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
        private bool followTimeScale;
        private bool systemPaused;
        private bool timePaused;
        private bool sourcePaused;

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

        #region Acoustic state
        // The AudioSource transform sits at the perceived position while an acoustic module asks for one, else at the logical position.
        private Vector3 logicalPosition;
        private bool hasPerceivedPosition;
        private AcousticState acoustic = AcousticState.Neutral;
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
        /// <summary> Prepared by Begin but not started: a queued voice line waiting for its emitter. It does not advance, sound or end. </summary>
        public bool IsReserved { get; private set; }
        public AudioAsset Asset { get; private set; }
        public AudioBus Bus { get; private set; }
        public int Priority { get; private set; }
        public float BaseVolume { get; private set; }
        public float Pitch { get; private set; }
        /// <summary> Paused through the handle. </summary>
        public bool UserPaused { get; private set; }
        /// <summary> Paused by the handle or by Audio.Pause (global or bus). Not by the time-scale pause. </summary>
        public bool IsPaused => UserPaused || systemPaused;
        /// <summary> Not advancing: paused, paused by the time scale, or paused by AudioListener.pause. </summary>
        public bool IsFrozen => IsReserved || IsPaused || timePaused || (AudioListener.pause && !source.ignoreListenerPause);
        /// <summary> True while the PlayDelayed delay has not elapsed yet (nothing is audible). </summary>
        public bool InDelay => elapsed < delay;
        public bool IsFadingOut => fadingOut;
        /// <summary> Current volume including fades, the handle multiplier and the bus multiplier. </summary>
        public float EffectiveVolume { get; private set; }
        /// <summary> Level the voice is heading to: base × handle volume × fade-out × bus, ignoring fade-in and the acoustic multiplier. Used to pick voices to steal, so a voice that is still fading in is not treated as silent. </summary>
        public float TargetVolume { get; private set; }
        /// <summary> Where the sound is, as the game set it (start position, At or follow target). The source transform may sit elsewhere (perceived position). </summary>
        public Vector3 LogicalPosition => logicalPosition;
        /// <summary> Smoothed acoustic state, written by the AcousticSimulator. </summary>
        public ref AcousticState Acoustic => ref acoustic;
        /// <summary> Current smoothed volume multiplier of the acoustic modules. </summary>
        public float AcousticVolume => acoustic.Volume;
        public Action<AudioHandle> EndedCallback { get; set; }
        /// <summary> Length of the current clip in seconds. </summary>
        public float ClipLength => clipLength;
        /// <summary> Clip chosen by the selection for the current playback. </summary>
        public AudioClip Clip { get; private set; }
        /// <summary> Length of the clip at the current pitch, without the delay. </summary>
        public float Duration => clipLength / Mathf.Max(0.01f, Mathf.Abs(Pitch));
        public bool IsLooping => source.loop;
        public bool Is3D => source.spatialBlend > 0f;
        /// <summary> Seconds of audio played: the elapsed time minus the delay. It does not advance while frozen. </summary>
        public float PlayedTime => Mathf.Max(0f, elapsed - delay);

        #region Effect cutoffs
        // The cutoffs the effect preset asked for, kept apart from the filters so the acoustic modules can combine them with their own.
        public bool HasEffectLowPass { get; private set; }
        public float EffectLowPassCutoff { get; private set; }
        public bool HasEffectHighPass { get; private set; }
        public float EffectHighPassCutoff { get; private set; }
        #endregion

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
            IsReserved = false;
            Asset = _asset;
            Bus = _asset.Bus;
            Priority = _asset.Priority;
            BaseVolume = _baseVolume;
            UserPaused = false;
            systemPaused = false;
            timePaused = false;
            sourcePaused = false;
            followTimeScale = _asset.FollowTimeScale;

            followTarget = _follow;
            following = _follow != null;
            SetLogicalPosition(_position);

            Clip = _clip;
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
            source.ignoreListenerPause = _asset.IgnorePause;
            SetPitch(_pitch);
        }

        /// <summary> Marks the prepared voice as waiting: it is not started until Activate. </summary>
        public void Reserve() => IsReserved = true;

        /// <summary> Starts a reserved voice. The caller then applies the current pause state with RefreshPause. </summary>
        public void Activate() {
            if (!IsReserved) return;
            IsReserved = false;
            StartSource();
        }

        public void SetEffectCutoffs(bool _lowPass, float _lowPassCutoff, bool _highPass, float _highPassCutoff) {
            HasEffectLowPass = _lowPass;
            EffectLowPassCutoff = _lowPassCutoff;
            HasEffectHighPass = _highPass;
            EffectHighPassCutoff = _highPassCutoff;
        }

        public void StartSource() {
            if (delay > 0f) source.PlayDelayed(delay);
            else source.Play();
        }

        /// <summary> Advances follow, fades and end detection. Returns true when the voice has finished and must be released. </summary>
        public bool Tick(float _delta, float _busMultiplier, bool _multiplyOnSource, bool _systemPaused) {
            if (following) {
                if (followTarget != null) SetLogicalPosition(followTarget.position);
                else {
                    // Target destroyed: keep the last position.
                    following = false;
                    followTarget = null;
                }
            }

            // A reserved voice only follows its target until it is activated.
            if (IsReserved) {
                systemPaused = _systemPaused;
                UpdateVolume(_busMultiplier, _multiplyOnSource);
                return false;
            }

            RefreshPause(_systemPaused);
            if (followTimeScale && !timePaused) ApplyPitch();

            // Paused voices (explicitly, by bus, by time scale or by AudioListener.pause) do not advance and never end.
            if (IsFrozen) {
                UpdateVolume(_busMultiplier, _multiplyOnSource);
                return false;
            }

            // Elapsed time is audio time: it runs slower with the pitch the time scale gives.
            elapsed += followTimeScale ? _delta * Time.timeScale : _delta;
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
            float _local = _target * _fadeIn * acoustic.Volume;
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
            ApplyPitch();
        }

        /// <summary> Applies the pause from Audio.Pause (global or bus) and the time-scale pause, then pauses or resumes the source. </summary>
        public void RefreshPause(bool _systemPaused) {
            systemPaused = _systemPaused;
            timePaused = followTimeScale && Time.timeScale <= 0f;
            SyncSource();
        }

        public void SetPosition(Vector3 _position) {
            following = false;
            followTarget = null;
            SetLogicalPosition(_position);
        }

        public void SetFollow(Transform _target) {
            followTarget = _target;
            following = _target != null;
            if (following) SetLogicalPosition(_target.position);
        }

        public void Pause() {
            if (UserPaused) return;
            UserPaused = true;
            SyncSource();
        }

        public void Resume() {
            if (!UserPaused) return;
            UserPaused = false;
            SyncSource();
        }

        /// <summary> Bumps the generation so every handle to the previous playback becomes stale. </summary>
        public void Invalidate(int _generation) => Generation = _generation;

        // ==================== POOL ===================
        void IPoolable.OnGetFromPool() { }

        void IPoolable.OnReleaseToPool() {
            IsActive = false;
            IsReserved = false;
            ResetComponents();
            SetEffectCutoffs(false, 0f, false, 0f);
            Asset = null;
            Clip = null;
            followTarget = null;
            following = false;
            EndedCallback = null;
            UserPaused = false;
            systemPaused = false;
            timePaused = false;
            sourcePaused = false;
            fadingOut = false;
            EffectiveVolume = 0f;
            TargetVolume = 0f;
            acoustic = AcousticState.Neutral;
            hasPerceivedPosition = false;
        }

        // ==================== ACOUSTICS ===================
        /// <summary> Moves the source to the perceived position, or back to the logical one. </summary>
        public void SetPerceivedPosition(bool _has, Vector3 _position) {
            hasPerceivedPosition = _has;
            Vector3 _target = _has ? _position : logicalPosition;
            if (transform.position != _target) transform.position = _target;
        }

        // ==================== PRIVATE ===================
        private void SetLogicalPosition(Vector3 _position) {
            logicalPosition = _position;
            if (!hasPerceivedPosition) transform.position = _position;
        }

        private void ApplyPitch() => source.pitch = followTimeScale ? Pitch * Time.timeScale : Pitch;

        // Pauses or resumes the AudioSource when the combined pause state changed.
        private void SyncSource() {
            // A reserved source never played: pausing or resuming it is left to Activate.
            if (IsReserved) return;
            bool _paused = UserPaused || systemPaused || timePaused;
            if (_paused == sourcePaused) return;
            sourcePaused = _paused;
            if (_paused) source.Pause();
            else {
                notPlayingTicks = 0;
                source.UnPause();
            }
        }

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
            // Neutral cutoffs and resonance: an acoustic module that later enables a filter only writes the cutoff.
            if (lowPass != null) {
                lowPass.enabled = false;
                lowPass.cutoffFrequency = AcousticResult.NeutralLowPass;
                lowPass.lowpassResonanceQ = 1f;
            }
            if (highPass != null) {
                highPass.enabled = false;
                highPass.cutoffFrequency = AcousticResult.NeutralHighPass;
                highPass.highpassResonanceQ = 1f;
            }
            if (distortion != null) distortion.enabled = false;
            if (echo != null) echo.enabled = false;
        }

        // Delay plus the clip length at the current pitch, measured from Begin.
        private float EstimatedDuration => delay + clipLength / Mathf.Max(0.01f, Mathf.Abs(Pitch));
    }
}
