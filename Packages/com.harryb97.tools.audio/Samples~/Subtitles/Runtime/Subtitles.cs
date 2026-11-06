using System;
using System.Collections.Generic;
using HarryTools.DomainReload;
using HarryTools.GameLoop;
using UnityEngine;

namespace HarryTools.Audio.Subtitles {
    /// <summary>
    /// Turns sounds and voice lines with a SubtitleExtension into subtitle events. It draws nothing: the game listens to
    /// Started, TextChanged and Ended and shows the lines its own way. Every Started gets exactly one Ended.
    /// </summary>
    public static class Subtitles {
        // ==================== VARIABLES ===================
        public const string LoopId = "HarryTools.Audio.Subtitles";
        public const float DefaultHearingDistance = 15f;
        public const float DefaultExitMargin = 0.1f;
        /// <summary> A playback that ends this close to its duration finished on its own; earlier, it was stopped. </summary>
        internal const float NaturalEndTolerance = 0.1f;

        private static readonly List<SubtitlePlayback> playbacks = new List<SubtitlePlayback>();
        private static readonly Stack<SubtitlePlayback> pool = new Stack<SubtitlePlayback>();
        private static readonly Action<AudioHandle> onAudioStarted = HandleStarted;
        private static readonly Action<AudioHandle> onAudioEnded = HandleEnded;
        private static readonly Action onLanguageChanged = HandleLanguageChanged;
        private static readonly LoopSystem.LoopDelegate tick = Tick;

        private static readonly Action<MusicAsset> onMusicChanged = HandleMusicChanged;

        private static ILoopable loop;
        // Changed is static in the core: any channel instance removes the subscription, so Shutdown never needs a live runtime.
        private static MusicChannel musicChannel;
        private static ISubtitleTextSource source;
        private static SubtitleSettings settings;
        private static int nextId;
        private static int nextRequest;
        private static ISubtitleSettingsStore store;
        // The store is read the first time an option is used, so booting never touches PlayerPrefs.
        private static bool preferencesLoaded;
        private static bool showDialogue = true;
        private static bool showCaptions = true;

        private static Action<SubtitleInfo> started;
        private static Action<SubtitleInfo> ended;
        private static Action<SubtitleInfo> textChanged;

        // ==================== EVENTS ===================
        /// <summary> A line appears. Raised once its text (and speaker name) is resolved. </summary>
        public static event Action<SubtitleInfo> Started {
            add => started += value;
            remove => started -= value;
        }

        /// <summary> A line disappears. Rely on this, not on Duration, to remove it. </summary>
        public static event Action<SubtitleInfo> Ended {
            add => ended += value;
            remove => ended -= value;
        }

        /// <summary> The language changed while the line was visible: new text and the time it has left. </summary>
        public static event Action<SubtitleInfo> TextChanged {
            add => textChanged += value;
            remove => textChanged -= value;
        }

        // ==================== PROPERTIES ===================
        /// <summary> Global settings. Null uses the defaults (15 m hearing distance, 0.1 exit margin). </summary>
        public static SubtitleSettings Settings {
            get => settings;
            set => settings = value;
        }

        /// <summary> Player option: show dialogue lines. On by default; saved in the store when it changes. </summary>
        public static bool ShowDialogue {
            get {
                EnsurePreferences();
                return showDialogue;
            }
            set {
                EnsurePreferences();
                if (showDialogue == value) return;
                showDialogue = value;
                SavePreferences();
            }
        }

        /// <summary> Player option: show captions (sound effects, ambience). On by default; saved in the store when it changes. </summary>
        public static bool ShowCaptions {
            get {
                EnsurePreferences();
                return showCaptions;
            }
            set {
                EnsurePreferences();
                if (showCaptions == value) return;
                showCaptions = value;
                SavePreferences();
            }
        }

        /// <summary> Text source in use; created on first use so Localization is not touched until a subtitle needs it. </summary>
        internal static ISubtitleTextSource Source {
            get {
                if (source == null) UseSource(new LocalizationTextSource());
                return source;
            }
        }

        /// <summary> Playbacks being tracked (playing, or stopped with lines still on time). </summary>
        internal static int ActivePlaybackCount => playbacks.Count;

        // ==================== PUBLIC ===================
        /// <summary> Where texts come from. Null goes back to Unity Localization. </summary>
        public static void SetTextSource(ISubtitleTextSource _source) {
            // Null is resolved lazily by Source, so clearing the source never touches Localization.
            UseSource(_source);
        }

        /// <summary> Where the player options are saved. Null uses PlayerPrefs. The saved values are loaded now (nothing saved keeps the current ones). </summary>
        public static void SetStore(ISubtitleSettingsStore _store) {
            store = _store ?? new PlayerPrefsSubtitleStore();
            LoadPreferences();
        }

        // ==================== LIFECYCLE ===================
        // Before the first scene loads, like the core: sounds started in the first scene's Awake/OnEnable must get subtitles.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap() => Boot(true);

        private static void Boot(bool _music) {
            Audio.Started -= onAudioStarted;
            Audio.Started += onAudioStarted;
            Audio.Ended -= onAudioEnded;
            Audio.Ended += onAudioEnded;

            if (LoopSystem.HasLoop(LoopId)) LoopSystem.RemoveLoop(LoopId);
            loop = new Loop(LoopId);
            loop.AddUpdateListener(tick);
            LoopSystem.AddLoop(loop);

            // Reading Audio.Music creates the runtime if needed, as the first Play would.
            if (_music) {
                musicChannel = Audio.Music;
                if (musicChannel != null) {
                    musicChannel.Changed -= onMusicChanged;
                    musicChannel.Changed += onMusicChanged;
                }
            }
        }

        [ExecuteOnReload]
        private static void ResetOnReload() {
            // Listeners of the previous Play session are gone: the open lines end silently.
            started = null;
            ended = null;
            textChanged = null;
            Shutdown();
            UseSource(null);
            settings = null;
            nextId = 0;
            nextRequest = 0;
            store = null;
            preferencesLoaded = false;
            showDialogue = true;
            showCaptions = true;
        }

        /// <summary> Ends every open line, drops the tracking and the pool, and subscribes again. Keeps listeners and the text source. </summary>
        internal static void ResetForTests(bool _subscribeToMusic = true) {
            Shutdown();
            Boot(_subscribeToMusic);
        }

        /// <summary> Ends every open line (each Started gets its Ended), stops listening to the audio and removes the loop. </summary>
        private static void Shutdown() {
            Audio.Started -= onAudioStarted;
            Audio.Ended -= onAudioEnded;
            if (musicChannel != null) {
                musicChannel.Changed -= onMusicChanged;
                musicChannel = null;
            }
            if (loop != null) {
                if (LoopSystem.TryGetLoop(LoopId, out ILoopable _registered) && _registered == loop) LoopSystem.RemoveLoop(LoopId);
                loop.RemoveUpdateListener(tick);
                loop = null;
            }

            // An Ended subscriber may start new playbacks: keep ending until the list is empty.
            while (playbacks.Count > 0) {
                int _last = playbacks.Count - 1;
                SubtitlePlayback _playback = playbacks[_last];
                playbacks.RemoveAt(_last);
                _playback.EndAll();
                _playback.Release();
                pool.Push(_playback);
            }
            pool.Clear();
        }

        // ==================== AUDIO ===================
        /// <summary> Starts tracking the playback when its asset has subtitles. Assets without them return before allocating anything. </summary>
        internal static void HandleStarted(AudioHandle _handle) {
            AudioAsset _asset = _handle.Asset;
            if (_asset == null) return;
            SubtitleExtension _extension = _asset.GetExtension<SubtitleExtension>();
            if (_extension == null) return;

            SubtitlePlayback _playback = pool.Count > 0 ? pool.Pop() : new SubtitlePlayback();
            playbacks.Add(_playback);
            _playback.Begin(_handle, _asset, _extension);
            // Entries at Delay 0 show with the sound, not one frame later.
            _playback.Advance();
        }

        private static void HandleEnded(AudioHandle _handle) {
            for (int _i = 0; _i < playbacks.Count; _i++) {
                if (playbacks[_i].IsMusic || playbacks[_i].Handle != _handle) continue;
                playbacks[_i].OnAudioEnded(NaturalEndTolerance);
                return;
            }
        }

        /// <summary>
        /// The current track changed. The previous track's lines end as a stop (a crossfade included); a non-looping track that
        /// ended on its own (null) is a natural end. A new track with subtitles starts being tracked.
        /// </summary>
        private static void HandleMusicChanged(MusicAsset _asset) {
            // Ended subscribers may play other music (a nested call that appends its own tracking): only the playbacks that existed are ended.
            int _existing = playbacks.Count;
            for (int _i = 0; _i < _existing && _i < playbacks.Count; _i++) {
                SubtitlePlayback _playback = playbacks[_i];
                if (_playback.IsMusic && !_playback.AudioEnded) _playback.OnAudioEnded(NaturalEndTolerance, _asset != null);
            }
            if (_asset == null) return;
            // A subscriber changed the track meanwhile: its own call tracks the new one.
            MusicChannel _channel = Audio.Music;
            if (_channel == null || _channel.Current != _asset) return;
            SubtitleExtension _extension = _asset.GetExtension<SubtitleExtension>();
            if (_extension == null) return;

            SubtitlePlayback _new = pool.Count > 0 ? pool.Pop() : new SubtitlePlayback();
            playbacks.Add(_new);
            // Changed means the runtime is alive; after a rebuild this is the new channel.
            _new.BeginMusic(_channel, _asset, _extension);
            _new.Advance();
        }

        private static void HandleLanguageChanged() {
            for (int _i = 0; _i < playbacks.Count; _i++) playbacks[_i].OnLanguageChanged();
        }

        internal static void Tick() {
            float _deltaTime = Time.unscaledDeltaTime;
            // By index: a subscriber may start sounds (appended) while the list is walked. Removal waits for the sweep below.
            for (int _i = 0; _i < playbacks.Count; _i++) {
                SubtitlePlayback _playback = playbacks[_i];
                if (!_playback.Tick(_deltaTime)) _playback.EndAll();
            }

            for (int _i = playbacks.Count - 1; _i >= 0; _i--) {
                SubtitlePlayback _playback = playbacks[_i];
                if (!_playback.IsFinished) continue;
                playbacks.RemoveAt(_i);
                _playback.Release();
                pool.Push(_playback);
            }
        }

        // ==================== INTERNAL ===================
        internal static int NextId() => ++nextId;

        internal static int NextRequest() {
            nextRequest++;
            if (nextRequest == 0) nextRequest = 1;
            return nextRequest;
        }

        internal static void RaiseStarted(SubtitleInfo _info) => Raise(started, _info);
        internal static void RaiseEnded(SubtitleInfo _info) => Raise(ended, _info);
        internal static void RaiseTextChanged(SubtitleInfo _info) => Raise(textChanged, _info);

        // ==================== PRIVATE ===================
        private static void EnsurePreferences() {
            if (!preferencesLoaded) LoadPreferences();
        }

        private static void LoadPreferences() {
            preferencesLoaded = true;
            if (store == null) store = new PlayerPrefsSubtitleStore();
            SubtitlePreferences? _saved = store.Load();
            if (!_saved.HasValue) return;
            showDialogue = _saved.Value.ShowDialogue;
            showCaptions = _saved.Value.ShowCaptions;
        }

        private static void SavePreferences() {
            store.Save(new SubtitlePreferences { ShowDialogue = showDialogue, ShowCaptions = showCaptions });
        }

        private static void UseSource(ISubtitleTextSource _source) {
            if (source == _source) return;
            if (source != null) source.LanguageChanged -= onLanguageChanged;
            source = _source;
            if (source != null) source.LanguageChanged += onLanguageChanged;
        }

        // A throwing subscriber must not break the timeline of the playback or the audio tick that raised the event.
        private static void Raise(Action<SubtitleInfo> _event, SubtitleInfo _info) {
            if (_event == null) return;
            try { _event(_info); } catch (Exception _exception) { Debug.LogException(_exception); }
        }
    }
}
