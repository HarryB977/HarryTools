using System;
using System.Collections.Generic;
using UnityEngine;

namespace HarryTools.Audio {
    /// <summary>
    /// The music player: one current track on two decks that crossfade. Intro→loop and loop points are chained sample-accurately
    /// with PlayScheduled (WebGL: Play and a full loop). Music follows Audio.Pause and the pause of its bus unless the asset has
    /// IgnorePause. Pitch is always 1: PitchRange and FollowTimeScale do not apply to music. Also drives the MusicZone components.
    /// </summary>
    public sealed class MusicChannel {
        // ==================== VARIABLES ===================
        /// <summary> Seconds between two MusicZone checks. </summary>
        internal const float ZoneInterval = 0.25f;

        private static readonly int BusCount = BusMixer.BusCount;

        private readonly AudioRuntime runtime;
        private readonly MusicDeck[] decks = new MusicDeck[2];
        private readonly bool[] activity = new bool[BusCount];
        private readonly bool[] reported = new bool[BusCount];
        private int current = -1;
        private bool userPaused;
        private Action<MusicAsset> changed;

        #region Zones
        private readonly List<MusicZone> inside = new List<MusicZone>(8);
        private float zoneTimer;
        private bool inZones;
        private MusicZone activeZone;
        private float activeZoneCrossfade;
        private MusicAsset beforeZones;
        #endregion

        // ==================== CONSTRUCTOR ===================
        internal MusicChannel(AudioRuntime _runtime, Transform _parent) {
            runtime = _runtime;
            decks[0] = new MusicDeck(_parent, "Music Deck 1");
            decks[1] = new MusicDeck(_parent, "Music Deck 2");
        }

        // ==================== EVENTS ===================
        /// <summary> Raised when the current track changes, with the new track (null after Stop or when a non-looping track ends). </summary>
        public event Action<MusicAsset> Changed {
            add => changed += value;
            remove => changed -= value;
        }

        // ==================== PROPERTIES ===================
        /// <summary> The current track, or null. A track that is fading out after Stop or a crossfade is not current. </summary>
        public MusicAsset Current => current >= 0 ? decks[current].Asset : null;

        /// <summary> True while there is a current track and it is not paused. </summary>
        public bool IsPlaying => current >= 0 && !decks[current].IsPaused;

        internal int CurrentDeckIndex => current;
        internal MusicDeck GetDeck(int _index) => decks[_index];

        // ==================== PUBLIC ===================
        /// <summary> Plays the track from the catalog. See Play(MusicAsset, float). </summary>
        public void Play(MusicRef _music, float _crossfade = -1f) {
            if (_music.TryResolve(out MusicAsset _asset)) {
                Play(_asset, _crossfade);
                return;
            }
            if (_music.IsValid) HarryTools.LoggerTool.Logger.LogWarning("Audio", "Music id " + _music.Id + " is not in the audio catalog.");
        }

        /// <summary>
        /// Crossfades to the track. A negative crossfade uses DefaultCrossfade of the settings (or the asset FadeIn when nothing is
        /// sounding); 0 switches at once. Does nothing when the track is already current; a track that is fading out comes back
        /// from where it is instead of restarting. Null stops the music with that fade.
        /// </summary>
        public void Play(MusicAsset _music, float _crossfade = -1f) {
            if (_music == null) {
                Stop(_crossfade);
                return;
            }
            if (current >= 0 && ReferenceEquals(decks[current].Asset, _music) && !decks[current].IsFadingOut) return;

            bool _audible = decks[0].IsActive || decks[1].IsActive;
            float _duration = _crossfade >= 0f ? _crossfade : _audible ? runtime.Settings.DefaultCrossfade : _music.FadeIn;

            int _target = FindDeck(_music);
            if (_target < 0) {
                if (MusicDeck.LoopClipOf(_music) == null) {
                    HarryTools.LoggerTool.Logger.LogWarning("Audio", "Music '" + _music.name + "' has no clip in Clips[0].");
                    return;
                }
                _target = FreeDeck();
                MusicDeck _deck = decks[_target];
                _deck.Begin(_music, runtime.GetOutput(_music), IsFrozen(_music));
                _deck.FadeTo(1f, _duration);
                _deck.ApplyVolume(runtime.Mixer.GetSourceMultiplier(_deck.Bus), runtime.MultiplyOnSources);
            } else decks[_target].FadeTo(1f, _duration);

            for (int _i = 0; _i < decks.Length; _i++) {
                if (_i != _target) FadeOutDeck(decks[_i], _duration);
            }
            SetCurrent(_target);
        }

        /// <summary> Stops the music. A negative fade uses the asset FadeOut; 0 (or paused music) stops at once. </summary>
        public void Stop(float _fade = -1f) {
            for (int _i = 0; _i < decks.Length; _i++) {
                MusicDeck _deck = decks[_i];
                if (!_deck.IsActive) continue;
                FadeOutDeck(_deck, _fade >= 0f ? _fade : _deck.Asset.FadeOut);
            }
            SetCurrent(-1);
        }

        /// <summary> Pauses the music, including IgnorePause tracks. Fades and scheduled joins freeze with it. </summary>
        public void Pause() {
            userPaused = true;
            RefreshPause();
        }

        public void Resume() {
            userPaused = false;
            RefreshPause();
        }

        // ==================== INTERNAL ===================
        internal void Tick(float _delta) {
            if (!runtime.IsHostAlive) return;
            bool _onSource = runtime.MultiplyOnSources;
            BusMixer _mixer = runtime.Mixer;
            for (int _i = 0; _i < BusCount; _i++) activity[_i] = false;

            for (int _i = 0; _i < decks.Length; _i++) {
                MusicDeck _deck = decks[_i];
                if (!_deck.IsActive) continue;
                if (_deck.Tick(_delta, _mixer.GetSourceMultiplier(_deck.Bus), _onSource, IsFrozen(_deck.Asset))) {
                    // Clear the current track first so Changed still sees which track ended.
                    if (_i == current) SetCurrent(-1);
                    _deck.Stop();
                    continue;
                }
                if (!_deck.IsPaused) activity[(int)_deck.Bus] = true;
            }

            // Only the buses music reported before are touched: other systems may report activity too.
            Ducker _ducker = runtime.Ducker;
            for (int _i = 0; _i < BusCount; _i++) {
                if (activity[_i] == reported[_i]) continue;
                reported[_i] = activity[_i];
                _ducker.SetExternalActivity((AudioBus)_i, activity[_i]);
            }

            zoneTimer += _delta;
            if (zoneTimer >= ZoneInterval) {
                zoneTimer = 0f;
                UpdateZones();
            }
        }

        internal void Dispose() {
            for (int _i = 0; _i < decks.Length; _i++) decks[_i].Stop();
            for (int _i = 0; _i < BusCount; _i++) {
                if (!reported[_i]) continue;
                reported[_i] = false;
                runtime.Ducker.SetExternalActivity((AudioBus)_i, false);
            }
            current = -1;
            inside.Clear();
            activeZone = null;
            beforeZones = null;
            changed = null;
        }

        // ==================== ZONES ===================
        // The zones the listener is in form a stack in entry order. The highest Priority wins; on a tie, the last one entered.
        // Leaving every zone brings back the track that was current before the first one.
        private void UpdateZones() {
            Transform _listener = runtime.ListenerTracker.Listener;
            if (_listener == null) return;
            Vector3 _position = _listener.position;

            for (int _i = inside.Count - 1; _i >= 0; _i--) {
                MusicZone _zone = inside[_i];
                if (_zone == null || !_zone.isActiveAndEnabled || !_zone.Contains(_position)) inside.RemoveAt(_i);
            }
            List<MusicZone> _registered = MusicZone.Registered;
            for (int _i = 0; _i < _registered.Count; _i++) {
                MusicZone _zone = _registered[_i];
                if (_zone == null || IsInside(_zone) || !_zone.Contains(_position)) continue;
                inside.Add(_zone);
            }

            MusicZone _best = null;
            for (int _i = 0; _i < inside.Count; _i++) {
                if (_best == null || inside[_i].Priority >= _best.Priority) _best = inside[_i];
            }
            if (ReferenceEquals(_best, activeZone)) return;

            if (_best != null) {
                if (!inZones) beforeZones = Current;
                inZones = true;
                activeZone = _best;
                activeZoneCrossfade = _best.Crossfade;
                Play(_best.Music, _best.Crossfade);
                return;
            }

            MusicAsset _restore = beforeZones;
            inZones = false;
            activeZone = null;
            beforeZones = null;
            if (_restore != null) Play(_restore, activeZoneCrossfade);
            else Stop(activeZoneCrossfade);
        }

        private bool IsInside(MusicZone _zone) {
            for (int _i = 0; _i < inside.Count; _i++) {
                if (ReferenceEquals(inside[_i], _zone)) return true;
            }
            return false;
        }

        // ==================== PRIVATE ===================
        // Explicit pause, Audio.Pause (global, bus or Master) unless IgnorePause, and AudioListener.pause unless the source ignores it.
        private bool IsFrozen(MusicAsset _asset) {
            if (userPaused) return true;
            if (_asset.IgnorePause) return false;
            if (AudioListener.pause) return true;
            VoiceManager _voices = runtime.Voices;
            return _voices.IsPaused || _voices.IsBusPaused(_asset.Bus) || _voices.IsBusPaused(AudioBus.Master);
        }

        private void RefreshPause() {
            for (int _i = 0; _i < decks.Length; _i++) {
                MusicDeck _deck = decks[_i];
                if (_deck.IsActive) _deck.SetPaused(IsFrozen(_deck.Asset));
            }
        }

        private int FindDeck(MusicAsset _music) {
            for (int _i = 0; _i < decks.Length; _i++) {
                if (ReferenceEquals(decks[_i].Asset, _music)) return _i;
            }
            return -1;
        }

        // An idle deck, else the quieter of the two (stopped at once to make room).
        private int FreeDeck() {
            for (int _i = 0; _i < decks.Length; _i++) {
                if (!decks[_i].IsActive) return _i;
            }
            int _victim = current == 0 ? 1 : current == 1 ? 0 : decks[0].FadeLevel <= decks[1].FadeLevel ? 0 : 1;
            decks[_victim].Stop();
            return _victim;
        }

        // Fades the deck out from its current level. A deck already fading out keeps its (shorter) fade; 0 or a paused deck stops at once.
        private static void FadeOutDeck(MusicDeck _deck, float _duration) {
            if (!_deck.IsActive) return;
            if (_duration <= 0f || _deck.IsPaused) {
                _deck.Stop();
                return;
            }
            if (!_deck.IsFadingOut) _deck.FadeTo(0f, _duration);
        }

        private void SetCurrent(int _index) {
            if (_index == current) return;
            MusicAsset _before = Current;
            current = _index;
            MusicAsset _after = Current;
            if (ReferenceEquals(_before, _after) || changed == null) return;
            try { changed(_after); } catch (Exception _exception) { Debug.LogException(_exception); }
        }
    }
}
