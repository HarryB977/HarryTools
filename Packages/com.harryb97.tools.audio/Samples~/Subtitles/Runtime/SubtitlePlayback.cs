using System;
using System.Collections.Generic;
using UnityEngine;

namespace HarryTools.Audio.Subtitles {
    /// <summary>
    /// Tracks one playback with subtitles: its timeline, the texts being resolved and the Id of each line on screen.
    /// Instances are pooled by Subtitles; Begin and Release recycle them.
    /// </summary>
    internal sealed class SubtitlePlayback : ISubtitleTimelineListener {
        // ==================== VARIABLES ===================
        private readonly SubtitleTimeline timeline = new SubtitleTimeline();

        #region Playback
        private AudioHandle handle;
        private AudioAsset asset;
        private SubtitleExtension extension;
        private IReadOnlyList<SubtitleEntry> entries;
        private SpeakerAsset speaker;
        private string speakerName = string.Empty;
        private float duration;
        private bool loop;
        private bool is3D;
        private Vector3 position;
        /// <summary> Music has no handle: its time comes from Audio.Music.Elapsed while this asset is the current track. </summary>
        private MusicAsset music;
        /// <summary> Channel the track was started on, read without going through Audio so a shut down runtime is never rebuilt by the tick. </summary>
        private MusicChannel musicChannel;
        /// <summary> Seconds of audio played: the handle Elapsed while it lives, then advanced by unscaled time after a stop. </summary>
        private float clock;
        private bool audioEnded;
        /// <summary> The listener is close enough to read this playback. Always true for 2D sounds and music. </summary>
        private bool audible;
        /// <summary> Request currently resolving the speaker name; 0 when it is resolved. </summary>
        private int nameRequest;
        #endregion

        #region Entries (indexed like the entries list)
        private int[] ids = Array.Empty<int>();
        private string[] texts = Array.Empty<string>();
        /// <summary> Request currently resolving the text; 0 when it is resolved. </summary>
        private int[] textRequests = Array.Empty<int>();
        private bool[] announced = Array.Empty<bool>();
        /// <summary> The language changed while the line was announced: TextChanged is due once the new text resolves. </summary>
        private bool[] refresh = Array.Empty<bool>();
        /// <summary> The audio stopped while the text was resolving: the line stays on the timeline but is never announced. </summary>
        private bool[] cancelled = Array.Empty<bool>();
        /// <summary> Started was emitted for this entry during this playback. Only such lines may show again after the audio ended. </summary>
        private bool[] everAnnounced = Array.Empty<bool>();
        private float[] shownAt = Array.Empty<float>();
        private float[] shownRemaining = Array.Empty<float>();
        /// <summary> Duration carried by the last Started or TextChanged, repeated in Ended. </summary>
        private float[] durations = Array.Empty<float>();
        #endregion

        // ==================== PROPERTIES ===================
        public AudioHandle Handle => handle;
        public bool IsMusic => music != null;
        /// <summary> The audio (or music track) has gone: stopped, finished or replaced. </summary>
        public bool AudioEnded => audioEnded;
        /// <summary> True when the audio is gone and no line can show any more: the playback can go back to the pool. </summary>
        public bool IsFinished => audioEnded && timeline.IsDone;

        // ==================== PUBLIC ===================
        public void Begin(AudioHandle _handle, AudioAsset _asset, SubtitleExtension _extension) {
            handle = _handle;
            music = null;
            Setup(_asset, _extension, _extension.GetEntries(_handle.Clip), _handle.Duration, _handle.IsLooping, _handle.Is3D, _handle.Position);
        }

        /// <summary> Tracks the music track the channel just made current. Its length is intro + loop clip when it does not loop. </summary>
        public void BeginMusic(MusicChannel _channel, MusicAsset _asset, SubtitleExtension _extension) {
            handle = AudioHandle.Invalid;
            music = _asset;
            musicChannel = _channel;
            AudioClip _loopClip = _asset.Clips.Count > 0 && _asset.Clips[0] != null ? _asset.Clips[0].Clip : null;
            AudioClip _intro = _asset.Intro;
            float _length = (_intro != null ? _intro.length : 0f) + (_loopClip != null ? _loopClip.length : 0f);
            Setup(_asset, _extension, _extension.GetEntries(_intro != null ? _intro : _loopClip), _length, _asset.Loop, false, Vector3.zero);
        }

        private void Setup(AudioAsset _asset, SubtitleExtension _extension, IReadOnlyList<SubtitleEntry> _entries, float _duration, bool _loop,
                           bool _is3D, Vector3 _position) {
            asset = _asset;
            extension = _extension;
            entries = _entries;
            duration = _duration;
            loop = _loop;
            is3D = _is3D;
            position = _position;
            clock = 0f;
            audioEnded = false;
            audible = false;

            int _count = entries != null ? entries.Count : 0;
            EnsureCapacity(_count);
            for (int _i = 0; _i < _count; _i++) ClearEntry(_i);

            speaker = (_asset as VoiceAsset)?.Speaker;
            speakerName = string.Empty;
            nameRequest = 0;
            timeline.Reset(entries, duration, loop);
            if (_count > 0) ResolveSpeakerName();
        }

        /// <summary> Reads the playback and moves the timeline. Returns false when the handle vanished without Ended (runtime rebuilt). </summary>
        public bool Tick(float _deltaTime) {
            if (audioEnded) {
                clock += _deltaTime;
                Advance();
                return true;
            }
            if (music != null) {
                // A disposed channel has no current track: the lines end.
                if (musicChannel.Current != music) return false;
                clock = musicChannel.Elapsed;
                Advance();
                return true;
            }
            if (!handle.IsValid) return false;
            clock = handle.Elapsed;
            position = handle.Position;
            // Lines are timed from when the sound starts being heard: nothing moves during the asset delay.
            if (handle.InDelay) return true;
            Advance();
            return true;
        }

        public void Advance() {
            UpdateAudible();
            timeline.Advance(clock, Subtitles.ShowDialogue && audible, Subtitles.ShowCaptions && audible, this);
        }

        /// <summary>
        /// The audio ended. A non-looping playback whose last seen time is within the tolerance of its duration finished:
        /// every line ends. Otherwise it was stopped and the extension EndOnStop decides.
        /// </summary>
        public void OnAudioEnded(float _naturalEndTolerance, bool _forceStop = false) {
            if (audioEnded) return;
            audioEnded = true;
            if (!_forceStop && !loop && duration - clock <= _naturalEndTolerance) timeline.Finish(this);
            else timeline.Stop(extension.EndOnStop, this);

            // A line kept by Stop whose text had not arrived yet would start after the audio: drop it.
            for (int _i = 0; _i < entries.Count; _i++) {
                if (!timeline.IsVisible(_i) || announced[_i]) continue;
                textRequests[_i] = 0;
                cancelled[_i] = true;
                // Never announced for the listener's ears: a later kind toggle must not bring it back as a new Started.
                everAnnounced[_i] = false;
            }
        }

        /// <summary> Ends every line now (runtime rebuilt or facade reset). </summary>
        public void EndAll() {
            audioEnded = true;
            timeline.Finish(this);
        }

        /// <summary> Resolves the visible lines and the speaker name again; announced lines raise TextChanged once both are back. </summary>
        public void OnLanguageChanged() {
            if (entries == null || timeline.IsDone) return;
            ResolveSpeakerName();
            for (int _i = 0; _i < entries.Count; _i++) {
                if (!timeline.IsVisible(_i) || cancelled[_i]) continue;
                if (announced[_i]) refresh[_i] = true;
                ResolveText(_i);
            }
        }

        public void Release() {
            int _count = entries != null ? entries.Count : 0;
            for (int _i = 0; _i < _count; _i++) ClearEntry(_i);
            handle = AudioHandle.Invalid;
            music = null;
            musicChannel = null;
            asset = null;
            extension = null;
            entries = null;
            speaker = null;
            speakerName = string.Empty;
            nameRequest = 0;
            audioEnded = true;
            audible = false;
            timeline.Reset(null, 0f, false);
        }

        // ==================== LISTENER ===================
        void ISubtitleTimelineListener.OnShow(int _entry, float _remaining) {
            ids[_entry] = 0;
            announced[_entry] = false;
            refresh[_entry] = false;
            shownAt[_entry] = clock;
            shownRemaining[_entry] = _remaining;
            // After the audio ended, only a line already announced (kept by Stop, then hidden and shown back by a kind toggle)
            // starts again, with its remaining time. A line never announced stays cancelled.
            cancelled[_entry] = audioEnded && !everAnnounced[_entry];
            if (cancelled[_entry]) {
                textRequests[_entry] = 0;
                return;
            }
            ResolveText(_entry);
        }

        void ISubtitleTimelineListener.OnHide(int _entry) {
            textRequests[_entry] = 0;
            refresh[_entry] = false;
            if (!announced[_entry]) return;
            announced[_entry] = false;
            Subtitles.RaiseEnded(CreateInfo(_entry, durations[_entry]));
        }

        // ==================== PRIVATE ===================
        /// <summary> Hysteresis: a 3D sound becomes audible at the hearing distance and stays so until distance x (1 + exit margin). No listener, not audible. </summary>
        private void UpdateAudible() {
            if (!is3D) {
                audible = true;
                return;
            }
            Transform _listener = Audio.Listener;
            if (_listener == null) {
                audible = false;
                return;
            }
            SubtitleSettings _settings = Subtitles.Settings;
            float _distance = extension.OverrideHearingDistance ? extension.HearingDistance
                : _settings != null ? _settings.HearingDistance : Subtitles.DefaultHearingDistance;
            if (audible) {
                float _margin = _settings != null ? _settings.ExitMargin : Subtitles.DefaultExitMargin;
                _distance *= 1f + _margin;
            }
            audible = (_listener.position - position).sqrMagnitude <= _distance * _distance;
        }

        private void ResolveText(int _entry) {
            int _request = Subtitles.NextRequest();
            textRequests[_entry] = _request;
            SubtitleEntry _data = entries[_entry];
            Subtitles.Source.Resolve(_data != null ? _data.Text : null, _text => OnTextResolved(_entry, _request, _text));
        }

        private void ResolveSpeakerName() {
            SpeakerSubtitleExtension _name = speaker != null ? speaker.GetExtension<SpeakerSubtitleExtension>() : null;
            if (_name == null || _name.Name == null) {
                nameRequest = 0;
                speakerName = string.Empty;
                return;
            }
            int _request = Subtitles.NextRequest();
            nameRequest = _request;
            Subtitles.Source.Resolve(_name.Name, _text => OnNameResolved(_request, _text));
        }

        private void OnTextResolved(int _entry, int _request, string _text) {
            if (entries == null || _entry >= entries.Count || textRequests[_entry] != _request) return;
            textRequests[_entry] = 0;
            texts[_entry] = _text ?? string.Empty;
            TryEmit(_entry);
        }

        private void OnNameResolved(int _request, string _text) {
            if (nameRequest != _request) return;
            nameRequest = 0;
            speakerName = _text ?? string.Empty;
            if (entries == null) return;
            for (int _i = 0; _i < entries.Count; _i++) TryEmit(_i);
        }

        /// <summary> Raises Started (or TextChanged after a language change) once the text and the speaker name are resolved. </summary>
        private void TryEmit(int _entry) {
            if (!timeline.IsVisible(_entry) || cancelled[_entry] || textRequests[_entry] != 0 || nameRequest != 0) return;
            if (!announced[_entry]) {
                // Marked first: a Started subscriber that stops the audio must get the Ended of this line.
                announced[_entry] = true;
                everAnnounced[_entry] = true;
                ids[_entry] = Subtitles.NextId();
                durations[_entry] = Remaining(_entry);
                Subtitles.RaiseStarted(CreateInfo(_entry, durations[_entry]));
                return;
            }
            if (!refresh[_entry]) return;
            refresh[_entry] = false;
            durations[_entry] = Remaining(_entry);
            Subtitles.RaiseTextChanged(CreateInfo(_entry, durations[_entry]));
        }

        /// <summary> Time the line has left: what the timeline gave on show minus the audio played since (the wait for the text included). </summary>
        private float Remaining(int _entry) {
            float _remaining = shownRemaining[_entry];
            if (_remaining < 0f) return -1f;
            return Mathf.Max(0f, _remaining - (clock - shownAt[_entry]));
        }

        private SubtitleInfo CreateInfo(int _entry, float _duration) {
            SubtitleEntry _data = entries[_entry];
            return new SubtitleInfo(ids[_entry], texts[_entry], _data != null ? _data.Kind : SubtitleKind.Dialogue, speaker, speakerName,
                                    asset, handle, _duration, position, is3D);
        }

        private void ClearEntry(int _entry) {
            ids[_entry] = 0;
            texts[_entry] = string.Empty;
            textRequests[_entry] = 0;
            announced[_entry] = false;
            refresh[_entry] = false;
            cancelled[_entry] = false;
            everAnnounced[_entry] = false;
            shownAt[_entry] = 0f;
            shownRemaining[_entry] = 0f;
            durations[_entry] = 0f;
        }

        private void EnsureCapacity(int _count) {
            if (ids.Length >= _count) return;
            ids = new int[_count];
            texts = new string[_count];
            textRequests = new int[_count];
            announced = new bool[_count];
            refresh = new bool[_count];
            cancelled = new bool[_count];
            everAnnounced = new bool[_count];
            shownAt = new float[_count];
            shownRemaining = new float[_count];
            durations = new float[_count];
        }
    }
}
