using System;
using UnityEngine;
using UnityEngine.Audio;

namespace HarryTools.Audio {
    /// <summary>
    /// One music track on two AudioSources of its own (outside the voice pool). With a mixer, the intro, the loop and the
    /// loop-point segments are chained sample-accurately with PlayScheduled on the DSP clock; without one (WebGL) the intro
    /// plays with Play() and the loop starts when the tick sees it end. Pitch is always 1.
    /// </summary>
    internal sealed class MusicDeck {
        // A source and the segment it has scheduled or is playing, in DSP time.
        private sealed class Track {
            public AudioSource Source;
            public AudioClip Clip;
            public bool Scheduled;
            public bool Loop;
            public bool Bounded;
            public int Offset;
            public double Start;
            public double End;
        }

        // ==================== VARIABLES ===================
        /// <summary> Seconds between a schedule call and the start it asks for, so the audio thread never receives a time in the past. </summary>
        internal const double ScheduleLead = 0.1;
        /// <summary> The next loop-point segment is scheduled when less than this many seconds remain. </summary>
        internal const double ScheduleAhead = 1.0;
        /// <summary> Loop-point ranges (LoopEnd − LoopStart) shorter than this many seconds are ignored and the whole clip loops instead: the two sources need time to alternate. </summary>
        internal const double MinLoopLength = 0.5;
        // Time after a segment end before its source is considered free, to never cut the last DSP buffer of the segment.
        private const double FreeMargin = 0.03;

        private readonly Track[] tracks = new Track[2];

        #region Track state
        private bool scheduledMode;
        private bool introPhase;
        private int loopTrack;
        private bool paused;
        private bool resumedThisTick;
        private double pausedAt;
        #endregion

        #region Loop points
        private bool loopPoints;
        private int nextTrack;
        private double cursor;
        private int nextOffset;
        private double nextLength;
        private int loopStartSamples;
        private int loopEndSamples;
        private double loopLength;
        #endregion

        #region Fade
        private float level;
        private float fadeFrom;
        private float fadeTo;
        private float fadeDuration;
        private float fadeElapsed;
        #endregion

        // ==================== CONSTRUCTOR ===================
        public MusicDeck(Transform _parent, string _name) {
            for (int _i = 0; _i < tracks.Length; _i++) {
                var _go = new GameObject(_name + (_i == 0 ? " A" : " B"));
                _go.transform.SetParent(_parent, false);
                var _source = _go.AddComponent<AudioSource>();
                _source.playOnAwake = false;
                tracks[_i] = new Track { Source = _source };
            }
        }

        // ==================== PROPERTIES ===================
        public MusicAsset Asset { get; private set; }
        public bool IsActive => !ReferenceEquals(Asset, null);
        public AudioBus Bus { get; private set; }
        /// <summary> Frozen by the channel: explicit pause, Audio.Pause (global or bus) or AudioListener.pause. </summary>
        public bool IsPaused => paused;
        /// <summary> Heading to silence; the deck stops when it gets there. </summary>
        public bool IsFadingOut { get; private set; }
        public float BaseVolume { get; private set; }
        /// <summary> Base volume × fade level, without the bus multiplier. </summary>
        public float Volume => BaseVolume * level;
        public float FadeLevel => level;
        /// <summary> Seconds played since the track began. It advances in Tick while the deck is not frozen and is not reset at the intro to loop join. </summary>
        public float Elapsed { get; private set; }
        /// <summary> Seconds left of the running fade (0 when none). </summary>
        public float FadeRemaining => Mathf.Max(0f, fadeDuration - fadeElapsed);

        // ==================== PUBLIC ===================
        public AudioSource GetSource(int _index) => tracks[_index].Source;
        /// <summary> DSP time the segment of the source starts (scheduled mode). </summary>
        public double GetStart(int _index) => tracks[_index].Start;
        /// <summary> DSP time the segment of the source ends; +∞ while it loops the whole clip. </summary>
        public double GetEnd(int _index) => tracks[_index].End;

        /// <summary> Starts the track at volume 0. False when the asset has no loop clip. </summary>
        public bool Begin(MusicAsset _asset, AudioMixerGroup _output, bool _frozen) {
            AudioClip _loopClip = LoopClipOf(_asset);
            if (_loopClip == null) return false;
            Stop();

            Asset = _asset;
            Bus = _asset.Bus;
            Vector2 _range = _asset.VolumeRange;
            BaseVolume = UnityEngine.Random.Range(_range.x, _range.y);
            level = fadeFrom = fadeTo = 0f;
            fadeDuration = fadeElapsed = 0f;
            IsFadingOut = false;
            for (int _i = 0; _i < tracks.Length; _i++) Configure(tracks[_i].Source, _asset, _output);

            AudioClip _intro = _asset.Intro;
            scheduledMode = AudioPlatform.UsesMixer;
            paused = _frozen;
            pausedAt = AudioSettings.dspTime;
            resumedThisTick = false;
            loopPoints = scheduledMode && _asset.Loop && ReadLoopPoints(_asset, _loopClip);

            if (scheduledMode) BeginScheduled(_intro, _loopClip);
            else BeginDirect(_intro, _loopClip);
            return true;
        }

        /// <summary> Moves the fade level linearly to the target over the duration (unscaled seconds). 0 = at once. </summary>
        public void FadeTo(float _target, float _duration) {
            IsFadingOut = _target <= 0f;
            if (_duration <= 0f) {
                level = fadeFrom = fadeTo = _target;
                fadeDuration = fadeElapsed = 0f;
                return;
            }
            fadeFrom = level;
            fadeTo = _target;
            fadeDuration = _duration;
            fadeElapsed = 0f;
        }

        /// <summary> Advances fades, the loop-point chain and end detection. Returns true when the deck finished (faded out or a non-looping track ended). </summary>
        public bool Tick(float _delta, float _busMultiplier, bool _multiplyOnSource, bool _frozen) {
            SetPaused(_frozen);
            if (!paused) {
                Elapsed += _delta;
                if (fadeElapsed < fadeDuration) {
                    fadeElapsed += _delta;
                    level = Mathf.Lerp(fadeFrom, fadeTo, fadeElapsed / fadeDuration);
                }
                if (IsFadingOut && level <= 0f) return true;
                if (scheduledMode ? TickScheduled() : TickDirect()) return true;
            }
            resumedThisTick = false;
            ApplyVolume(_busMultiplier, _multiplyOnSource);
            return false;
        }

        public void ApplyVolume(float _busMultiplier, bool _multiplyOnSource) {
            float _volume = BaseVolume * level;
            if (_multiplyOnSource) _volume *= _busMultiplier;
            for (int _i = 0; _i < tracks.Length; _i++) tracks[_i].Source.volume = _volume;
        }

        /// <summary>
        /// Freezes or resumes the deck. With scheduled playback the sources are stopped and, on resume, every segment is
        /// re-scheduled from the recorded positions relative to the DSP clock, so an intro→loop or loop-point join stays sample-accurate.
        /// </summary>
        public void SetPaused(bool _paused) {
            if (_paused == paused || !IsActive) return;
            paused = _paused;
            if (_paused) {
                pausedAt = AudioSettings.dspTime;
                for (int _i = 0; _i < tracks.Length; _i++) {
                    Track _track = tracks[_i];
                    if (scheduledMode) {
                        if (_track.Scheduled) _track.Source.Stop();
                    } else _track.Source.Pause();
                }
                return;
            }

            resumedThisTick = true;
            if (!scheduledMode) {
                for (int _i = 0; _i < tracks.Length; _i++) tracks[_i].Source.UnPause();
                return;
            }

            double _restart = AudioSettings.dspTime + ScheduleLead;
            double _shift = _restart - pausedAt;
            for (int _i = 0; _i < tracks.Length; _i++) {
                Track _track = tracks[_i];
                if (!_track.Scheduled) continue;
                if (_track.End <= pausedAt) {
                    _track.Scheduled = false;
                    continue;
                }
                if (_track.Start < pausedAt) {
                    // Was playing: continue from where it was when paused.
                    long _offset = _track.Offset + (long)Math.Round((pausedAt - _track.Start) * _track.Clip.frequency);
                    if (_track.Loop) _offset %= _track.Clip.samples;
                    _track.Offset = (int)Math.Min(_offset, _track.Clip.samples - 1);
                    _track.Start = _restart;
                } else _track.Start += _shift;
                _track.End += _shift;
                Apply(_track);
            }
            cursor += _shift;
        }

        /// <summary> Stops both sources at once and frees the deck. </summary>
        public void Stop() {
            for (int _i = 0; _i < tracks.Length; _i++) {
                Track _track = tracks[_i];
                // The host (and the sources with it) can be destroyed from outside before the runtime shuts down.
                if (_track.Source != null) {
                    _track.Source.Stop();
                    _track.Source.clip = null;
                    _track.Source.loop = false;
                }
                _track.Clip = null;
                _track.Scheduled = false;
                _track.Start = _track.End = 0d;
            }
            Asset = null;
            paused = false;
            introPhase = false;
            loopPoints = false;
            IsFadingOut = false;
            Elapsed = 0f;
            level = fadeFrom = fadeTo = 0f;
            fadeDuration = fadeElapsed = 0f;
        }

        /// <summary> The clip the track loops: Clips[0]. </summary>
        public static AudioClip LoopClipOf(MusicAsset _asset) {
            if (_asset == null) return null;
            var _clips = _asset.Clips;
            return _clips.Count > 0 && _clips[0] != null ? _clips[0].Clip : null;
        }

        // ==================== SCHEDULED ===================
        // Intro on A at t0; then either B looping the whole clip (scheduled right away at the intro end) or the loop-point chain.
        private void BeginScheduled(AudioClip _intro, AudioClip _loopClip) {
            double _t0 = AudioSettings.dspTime + ScheduleLead;
            int _next = 0;
            double _at = _t0;
            if (_intro != null) {
                Schedule(tracks[0], _intro, 0, _t0, _t0 + _intro.samples / (double)_intro.frequency, false, false);
                _at = tracks[0].End;
                _next = 1;
            }

            if (loopPoints) {
                nextTrack = _next;
                cursor = _at;
                nextOffset = 0;
                nextLength = loopEndSamples / (double)_loopClip.frequency;
                ScheduleLoopSegments(AudioSettings.dspTime);
                return;
            }

            loopTrack = _next;
            bool _loop = Asset.Loop;
            double _end = _loop ? double.PositiveInfinity : _at + _loopClip.samples / (double)_loopClip.frequency;
            Schedule(tracks[_next], _loopClip, 0, _at, _end, _loop, false);
        }

        private bool TickScheduled() {
            double _dsp = AudioSettings.dspTime;
            if (resumedThisTick) return false;
            if (loopPoints) {
                ScheduleLoopSegments(_dsp);
                return false;
            }
            bool _any = false;
            for (int _i = 0; _i < tracks.Length; _i++) {
                Track _track = tracks[_i];
                if (_track.Scheduled && _track.End <= _dsp) _track.Scheduled = false;
                _any |= _track.Scheduled;
            }
            return !_any;
        }

        // Alternates the two sources: each plays [offset, offset + length] and stops on its scheduled end, exactly when the other starts.
        private void ScheduleLoopSegments(double _dsp) {
            if (paused) return;
            for (int _i = 0; _i < tracks.Length; _i++) {
                Track _track = tracks[_i];
                if (_track.Scheduled && _track.End + FreeMargin <= _dsp) _track.Scheduled = false;
            }
            for (int _guard = 0; _guard < tracks.Length; _guard++) {
                if (cursor - _dsp >= ScheduleAhead) return;
                Track _track = tracks[nextTrack];
                if (_track.Scheduled) return;
                // A hitch made us miss the join: start again a little ahead (audible gap, but the chain keeps going).
                if (cursor < _dsp + 0.01) cursor = _dsp + ScheduleLead;
                Schedule(_track, LoopClipOf(Asset), nextOffset, cursor, cursor + nextLength, false, true);
                cursor += nextLength;
                nextTrack = 1 - nextTrack;
                nextOffset = loopStartSamples;
                nextLength = loopLength;
            }
        }

        private void Schedule(Track _track, AudioClip _clip, int _offset, double _start, double _end, bool _loop, bool _bounded) {
            _track.Clip = _clip;
            _track.Offset = _offset;
            _track.Start = _start;
            _track.End = _end;
            _track.Loop = _loop;
            _track.Bounded = _bounded;
            _track.Scheduled = true;
            if (!paused) Apply(_track);
        }

        private static void Apply(Track _track) {
            AudioSource _source = _track.Source;
            _source.Stop();
            _source.clip = _track.Clip;
            _source.loop = _track.Loop;
            _source.timeSamples = _track.Offset;
            _source.PlayScheduled(_track.Start);
            if (_track.Bounded) _source.SetScheduledEndTime(_track.End);
        }

        // Loop points in samples of the loop clip. LoopEnd 0 or past the clip means the clip end. False (full loop) when the range is too short.
        private bool ReadLoopPoints(MusicAsset _asset, AudioClip _clip) {
            if (!_asset.UseLoopPoints) return false;
            int _frequency = _clip.frequency;
            long _end = _asset.LoopEnd > 0d ? (long)Math.Round(_asset.LoopEnd * _frequency) : _clip.samples;
            if (_end > _clip.samples) _end = _clip.samples;
            long _start = (long)Math.Round(_asset.LoopStart * _frequency);
            if ((_end - _start) / (double)_frequency < MinLoopLength) {
                HarryTools.LoggerTool.Logger.LogWarning("Audio", "Music '" + _asset.name + "': loop range (LoopEnd - LoopStart) is shorter than the minimum of " + MinLoopLength + " s, so the loop points are ignored and the whole clip loops.");
                return false;
            }
            loopStartSamples = (int)_start;
            loopEndSamples = (int)_end;
            loopLength = (_end - _start) / (double)_frequency;
            return true;
        }

        // ==================== DIRECT (NO MIXER) ===================
        private void BeginDirect(AudioClip _intro, AudioClip _loopClip) {
            introPhase = _intro != null;
            loopTrack = introPhase ? 1 : 0;
            tracks[loopTrack].Clip = _loopClip;
            if (introPhase) StartDirect(tracks[0], _intro, false);
            else StartDirect(tracks[0], _loopClip, Asset.Loop);
        }

        private bool TickDirect() {
            if (resumedThisTick) return false;
            if (introPhase) {
                if (tracks[0].Source.isPlaying) return false;
                introPhase = false;
                tracks[0].Scheduled = false;
                StartDirect(tracks[loopTrack], tracks[loopTrack].Clip, Asset.Loop);
                return false;
            }
            return !tracks[loopTrack].Source.isPlaying;
        }

        private void StartDirect(Track _track, AudioClip _clip, bool _loop) {
            _track.Clip = _clip;
            _track.Loop = _loop;
            _track.Scheduled = true;
            AudioSource _source = _track.Source;
            _source.clip = _clip;
            _source.loop = _loop;
            _source.timeSamples = 0;
            _source.Play();
            if (paused) _source.Pause();
        }

        // ==================== PRIVATE ===================
        private static void Configure(AudioSource _source, MusicAsset _asset, AudioMixerGroup _output) {
            _source.Stop();
            _source.outputAudioMixerGroup = _output;
            _source.ignoreListenerPause = _asset.IgnorePause;
            _source.priority = Mathf.Clamp(256 - _asset.Priority * 256 / 100, 0, 256);
            _source.pitch = 1f;
            _source.spatialBlend = 0f;
            _source.panStereo = 0f;
            _source.volume = 0f;
            _source.loop = false;
        }
    }
}
