using System;
using System.Collections.Generic;

namespace HarryTools.Audio.Subtitles {
    /// <summary>
    /// Decides which subtitle entries of one playback are visible at each moment. It holds no references to the entries:
    /// Reset copies delay, kind and end of each one, so the caller keeps the texts. Advance does not allocate and Reset
    /// reuses its arrays once they are big enough.
    /// </summary>
    internal sealed class SubtitleTimeline {
        // ==================== VARIABLES ===================
        private enum EntryState : byte { Pending, Active, Done }

        #region Entries (indexed by the position in the list passed to Reset)
        private float[] starts = Array.Empty<float>();
        private float[] ends = Array.Empty<float>();
        private SubtitleKind[] kinds = Array.Empty<SubtitleKind>();
        private EntryState[] states = Array.Empty<EntryState>();
        private bool[] visible = Array.Empty<bool>();
        #endregion

        /// <summary> Entry indices sorted by Delay, stable on ties. </summary>
        private int[] order = Array.Empty<int>();
        private int count;
        private bool stopped;

        // ==================== PROPERTIES ===================
        /// <summary> True when no entry is visible and none can still show. </summary>
        public bool IsDone {
            get {
                for (int _i = 0; _i < count; _i++) {
                    if (states[_i] != EntryState.Done) return false;
                }
                return true;
            }
        }

        // ==================== PUBLIC ===================
        /// <summary>
        /// Prepares the timeline for a new playback. The end of each entry is the earliest of: the Delay of the next entry
        /// of the same kind, Delay + Duration when Duration is above 0, and the audio duration when the sound does not loop.
        /// </summary>
        public void Reset(IReadOnlyList<SubtitleEntry> _entries, float _audioDuration, bool _loop) {
            count = _entries != null ? _entries.Count : 0;
            stopped = false;
            EnsureCapacity(count);

            for (int _i = 0; _i < count; _i++) {
                SubtitleEntry _entry = _entries[_i];
                starts[_i] = _entry != null ? _entry.Delay : 0f;
                kinds[_i] = _entry != null ? _entry.Kind : SubtitleKind.Dialogue;
                states[_i] = _entry != null ? EntryState.Pending : EntryState.Done;
                visible[_i] = false;
                order[_i] = _i;
            }

            SortOrderByStart();

            for (int _o = 0; _o < count; _o++) {
                int _index = order[_o];
                if (states[_index] == EntryState.Done) continue;
                float _end = float.PositiveInfinity;
                for (int _n = _o + 1; _n < count; _n++) {
                    int _next = order[_n];
                    if (states[_next] != EntryState.Done && kinds[_next] == kinds[_index]) {
                        _end = starts[_next];
                        break;
                    }
                }
                float _duration = _entries[_index].Duration;
                if (_duration > 0f) _end = Math.Min(_end, starts[_index] + _duration);
                if (!_loop) _end = Math.Min(_end, _audioDuration);
                ends[_index] = _end;
            }
        }

        /// <summary>
        /// Moves the timeline to the elapsed time. Hides first (entries past their end or of a disabled kind), then shows
        /// entries inside their window whose kind is enabled, with the time they have left.
        /// </summary>
        public void Advance(float _elapsed, bool _showDialogue, bool _showCaptions, ISubtitleTimelineListener _listener) {
            for (int _o = 0; _o < count; _o++) {
                int _index = order[_o];
                if (states[_index] == EntryState.Done) continue;

                if (_elapsed >= ends[_index]) {
                    states[_index] = EntryState.Done;
                    Hide(_index, _listener);
                    continue;
                }

                if (_elapsed < starts[_index]) continue;
                states[_index] = EntryState.Active;
                if (visible[_index] && !IsKindShown(kinds[_index], _showDialogue, _showCaptions)) {
                    Hide(_index, _listener);
                    // After Stop only the visible entries may finish: a hidden one is over.
                    if (stopped) states[_index] = EntryState.Done;
                }
            }

            if (stopped) return;
            for (int _o = 0; _o < count; _o++) {
                int _index = order[_o];
                if (states[_index] != EntryState.Active || visible[_index]) continue;
                if (!IsKindShown(kinds[_index], _showDialogue, _showCaptions)) continue;
                visible[_index] = true;
                float _end = ends[_index];
                _listener?.OnShow(_index, float.IsPositiveInfinity(_end) ? -1f : _end - _elapsed);
            }
        }

        /// <summary>
        /// The audio stopped before its time. With _endVisible every visible entry hides now; otherwise the visible ones
        /// keep their end (the caller goes on calling Advance with the time that passes). Pending entries are cancelled
        /// either way, and so are entries that only end when a loop stops.
        /// </summary>
        public void Stop(bool _endVisible, ISubtitleTimelineListener _listener) {
            stopped = true;
            for (int _o = 0; _o < count; _o++) {
                int _index = order[_o];
                if (states[_index] == EntryState.Done) continue;
                if (visible[_index] && !_endVisible && !float.IsPositiveInfinity(ends[_index])) continue;
                states[_index] = EntryState.Done;
                Hide(_index, _listener);
            }
        }

        /// <summary> The audio ended: hides every visible entry and cancels the rest. </summary>
        public void Finish(ISubtitleTimelineListener _listener) {
            Stop(true, _listener);
        }

        /// <summary> True while the entry (index in the list passed to Reset) is shown. </summary>
        public bool IsVisible(int _entry) {
            return _entry >= 0 && _entry < count && visible[_entry];
        }

        // ==================== PRIVATE ===================
        private void Hide(int _index, ISubtitleTimelineListener _listener) {
            if (!visible[_index]) return;
            visible[_index] = false;
            _listener?.OnHide(_index);
        }

        private static bool IsKindShown(SubtitleKind _kind, bool _showDialogue, bool _showCaptions) {
            return _kind == SubtitleKind.Caption ? _showCaptions : _showDialogue;
        }

        /// <summary> Insertion sort: stable, allocation-free and fast for the handful of entries a sound has. </summary>
        private void SortOrderByStart() {
            for (int _i = 1; _i < count; _i++) {
                int _current = order[_i];
                float _start = starts[_current];
                int _j = _i - 1;
                while (_j >= 0 && starts[order[_j]] > _start) {
                    order[_j + 1] = order[_j];
                    _j--;
                }
                order[_j + 1] = _current;
            }
        }

        private void EnsureCapacity(int _count) {
            if (order.Length >= _count) return;
            starts = new float[_count];
            ends = new float[_count];
            kinds = new SubtitleKind[_count];
            states = new EntryState[_count];
            visible = new bool[_count];
            order = new int[_count];
        }
    }
}
