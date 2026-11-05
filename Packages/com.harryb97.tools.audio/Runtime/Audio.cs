using System;
using HarryTools.DomainReload;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HarryTools.Audio {
    /// <summary>
    /// Entry point of the audio system. The runtime is created before the first scene loads when the project has audio settings,
    /// or on the first call otherwise. It only exists in Play mode.
    /// </summary>
    public static class Audio {
        // ==================== VARIABLES ===================
        private static AudioRuntime runtime;
        private static bool quitting;
        private static Action<AudioHandle> started;
        private static Action<AudioHandle> ended;
        private static AudioProjectSettings ownedSettings;

        // ==================== EVENTS ===================
        /// <summary> Raised when a sound starts (after Play returns a valid handle). </summary>
        public static event Action<AudioHandle> Started {
            add => started += value;
            remove => started -= value;
        }

        /// <summary> Raised when a sound ends for any reason: finished, stopped or stolen. The handle is already invalid. </summary>
        public static event Action<AudioHandle> Ended {
            add => ended += value;
            remove => ended -= value;
        }

        // ==================== PROPERTIES ===================
        /// <summary> The live runtime, created on first use. Null outside Play mode or while the application quits. </summary>
        internal static AudioRuntime Runtime {
            get {
                if (Live == null) Create(null);
                return runtime;
            }
        }

        // The runtime if its host object is still alive. A host destroyed from outside shuts the runtime down so the next use rebuilds it.
        private static AudioRuntime Live {
            get {
                if (runtime != null && !runtime.IsHostAlive) Shutdown();
                return runtime;
            }
        }

        /// <summary> The live runtime without creating it. Handles use this. </summary>
        internal static AudioRuntime Current => runtime;

        /// <summary> Transform sounds are heard from: the one set with SetListener, else the active AudioListener. </summary>
        public static Transform Listener => Runtime?.ListenerTracker.Listener;

        /// <summary> The music channel: one track at a time with crossfades, intro and loop points. Null outside Play mode. </summary>
        public static MusicChannel Music => Runtime?.Music;

        /// <summary> Voice lines and conversations, said through AudioEmitters or in 2D. Null outside Play mode. </summary>
        public static VoiceChannel Voices => Runtime?.VoiceChannel;

        // ==================== PLAY ===================
        /// <summary> Plays the sound at the listener, following it (world origin when there is no listener). </summary>
        public static AudioHandle Play(SoundRef _sound) => Resolve(_sound, out SoundAsset _asset) ? Play(_asset) : AudioHandle.Invalid;
        public static AudioHandle Play(SoundRef _sound, Vector3 _position) => Resolve(_sound, out SoundAsset _asset) ? Play(_asset, _position) : AudioHandle.Invalid;
        public static AudioHandle Play(SoundRef _sound, Transform _follow) => Resolve(_sound, out SoundAsset _asset) ? Play(_asset, _follow) : AudioHandle.Invalid;

        /// <summary> Plays the sound at the listener, following it (world origin when there is no listener). </summary>
        public static AudioHandle Play(SoundAsset _sound) {
            if (_sound == null) return AudioHandle.Invalid;
            AudioRuntime _runtime = Runtime;
            Transform _listener = _runtime != null ? _runtime.ListenerTracker.Listener : null;
            return Start(new PlaybackRequest(_sound, Vector3.zero, _listener));
        }
        public static AudioHandle Play(SoundAsset _sound, Vector3 _position) => Start(new PlaybackRequest(_sound, _position));
        public static AudioHandle Play(SoundAsset _sound, Transform _follow) => Start(new PlaybackRequest(_sound, Vector3.zero, _follow));

        // ==================== STOP ===================
        /// <summary> Stops every instance of the sound. A negative fade uses the asset FadeOut; 0 stops at once. </summary>
        public static void Stop(SoundAsset _sound, float _fade = -1f) {
            if (_sound == null) return;
            Live?.Voices.StopAll(_sound, _fade);
        }

        /// <summary> Stops every sound. A negative fade uses each asset FadeOut. </summary>
        public static void StopAll(float _fade = 0f) => Live?.Voices.StopAll(null, _fade);

        // ==================== PAUSE ===================
        /// <summary> Pauses every sound whose asset does not have IgnorePause. Does not touch AudioListener.pause. </summary>
        public static void Pause() => Runtime?.Voices.SetPaused(true);
        public static void Resume() => Live?.Voices.SetPaused(false);
        public static bool IsPaused => Live != null && runtime.Voices.IsPaused;

        /// <summary> Pauses the sounds of a bus. Pausing Master pauses every bus; IsBusPaused and Resume of another bus neither reflect nor lift it. Combines with the global pause. </summary>
        public static void Pause(AudioBus _bus) => Runtime?.Voices.SetBusPaused(_bus, true);
        public static void Resume(AudioBus _bus) => Live?.Voices.SetBusPaused(_bus, false);
        public static bool IsBusPaused(AudioBus _bus) => Live != null && runtime.Voices.IsBusPaused(_bus);

        // ==================== VOLUME ===================
        /// <summary> Sets the linear volume (0..1) of a bus. Saved to the store 0.5 s after the last change (and on shutdown). </summary>
        public static void SetVolume(AudioBus _bus, float _linear) {
            AudioRuntime _runtime = Runtime;
            if (_runtime == null) return;
            _runtime.Mixer.SetVolume(_bus, _linear);
            _runtime.MarkVolumesChanged();
        }

        public static float GetVolume(AudioBus _bus) {
            AudioRuntime _runtime = Runtime;
            return _runtime != null ? _runtime.Mixer.GetVolume(_bus) : 1f;
        }

        /// <summary> Mutes or unmutes a bus without losing its volume. Saved like SetVolume. </summary>
        public static void SetMuted(AudioBus _bus, bool _muted) {
            AudioRuntime _runtime = Runtime;
            if (_runtime == null) return;
            _runtime.Mixer.SetMuted(_bus, _muted);
            _runtime.MarkVolumesChanged();
        }

        public static bool IsMuted(AudioBus _bus) {
            AudioRuntime _runtime = Runtime;
            return _runtime != null && _runtime.Mixer.IsMuted(_bus);
        }

        /// <summary> Where bus volumes are saved. Defaults to PlayerPrefs with the settings PersistenceKey. Loads the saved volumes when assigned. </summary>
        public static void SetStore(IAudioSettingsStore _store) => Runtime?.SetStore(_store);

        /// <summary> Overrides the listener transform. Null goes back to the active AudioListener. </summary>
        public static void SetListener(Transform _listener) => Runtime?.ListenerTracker.Set(_listener);

        // ==================== TESTS ===================
        /// <summary> Tears down the current runtime and builds a fresh one from the given settings. </summary>
        internal static void InitializeForTests(AudioProjectSettings _settings) {
            Shutdown();
            quitting = false;
            Create(_settings);
        }

        internal static void ShutdownForTests() => Shutdown();

        // ==================== LIFECYCLE ===================
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap() {
            quitting = false;
            Application.quitting -= OnQuitting;
            Application.quitting += OnQuitting;
            if (runtime == null && AudioProjectSettings.Instance != null) Create(null);
        }

        [ExecuteOnReload]
        private static void ResetOnReload() {
            Shutdown();
            quitting = false;
            started = null;
            ended = null;
        }

        private static void OnQuitting() {
            quitting = true;
            Shutdown();
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void RegisterEditorHooks() {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        private static void OnPlayModeChanged(PlayModeStateChange _change) {
            if (_change == PlayModeStateChange.ExitingPlayMode) Shutdown();
        }
#endif

        private static void Create(AudioProjectSettings _settings) {
            if (runtime != null || quitting || !Application.isPlaying) return;
            if (_settings == null) _settings = AudioProjectSettings.Instance;
            if (_settings == null) {
                // No settings asset: run with the defaults.
                ownedSettings = ScriptableObject.CreateInstance<AudioProjectSettings>();
                ownedSettings.hideFlags = HideFlags.HideAndDontSave;
                _settings = ownedSettings;
            }
            runtime = new AudioRuntime(_settings, RaiseStarted, RaiseEnded);
        }

        private static void Shutdown() {
            AudioRuntime _runtime = runtime;
            runtime = null;
            _runtime?.Dispose();
            if (ownedSettings != null) {
                if (Application.isPlaying) UnityEngine.Object.Destroy(ownedSettings);
                else UnityEngine.Object.DestroyImmediate(ownedSettings);
            }
            ownedSettings = null;
        }

        // ==================== PRIVATE ===================
        private static AudioHandle Start(in PlaybackRequest _request) {
            if (_request.Asset == null) return AudioHandle.Invalid;
            AudioRuntime _runtime = Runtime;
            return _runtime != null ? _runtime.Voices.Start(_request) : AudioHandle.Invalid;
        }

        private static bool Resolve(SoundRef _sound, out SoundAsset _asset) {
            if (_sound.TryResolve(out _asset)) return true;
            if (_sound.IsValid) HarryTools.LoggerTool.Logger.LogWarning("Audio", "Sound id " + _sound.Id + " is not in the audio catalog.");
            return false;
        }

        private static void RaiseStarted(AudioHandle _handle) => started?.Invoke(_handle);
        private static void RaiseEnded(AudioHandle _handle) => ended?.Invoke(_handle);
    }
}
