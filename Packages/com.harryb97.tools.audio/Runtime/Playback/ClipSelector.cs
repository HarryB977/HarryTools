using UnityEngine;

namespace HarryTools.Audio {
    /// <summary> Per-asset runtime state that picks the next clip. Snapshots the asset clips on creation and never allocates in Next(). </summary>
    internal sealed class ClipSelector {
        // ==================== VARIABLES ===================
        private readonly ClipSelection selection;
        private readonly System.Random rng;
        private readonly AudioClip[] clips;
        private readonly float[] weights;
        private readonly int[] bag;
        private readonly int count;
        private int last = -1;
        private int bagPos;

        // ==================== CONSTRUCTORS ===================
        public ClipSelector(AudioAsset _asset, System.Random _rng) {
            rng = _rng;
            selection = _asset.Selection;
            var _entries = _asset.Clips;
            clips = new AudioClip[_entries.Count];
            weights = new float[_entries.Count];
            bag = new int[_entries.Count];
            int _valid = 0;
            for (int _i = 0; _i < _entries.Count; _i++) {
                if (_entries[_i].Clip == null) continue;
                clips[_valid] = _entries[_i].Clip;
                weights[_valid] = Mathf.Max(0f, _entries[_i].Weight);
                _valid++;
            }
            count = _valid;
            bagPos = count;
        }

        // ==================== PUBLIC ===================
        /// <summary> Next clip according to the selection mode, or null when there are no valid clips. </summary>
        public AudioClip Next() {
            if (count == 0) return null;
            if (count == 1) { last = 0; return clips[0]; }

            int _index;
            switch (selection) {
                case ClipSelection.RandomNoRepeat: _index = PickWeighted(last); break;
                case ClipSelection.Shuffle: _index = NextShuffle(); break;
                case ClipSelection.Sequence: _index = (last + 1) % count; break;
                default: _index = PickWeighted(-1); break;
            }
            last = _index;
            return clips[_index];
        }

        // ==================== PRIVATE ===================
        private int PickWeighted(int _exclude) {
            float _total = 0f;
            for (int _i = 0; _i < count; _i++) {
                if (_i != _exclude) _total += weights[_i];
            }
            if (_total <= 0f) {
                // No weighted candidate: fall back to uniform among the allowed ones.
                int _pick = rng.Next(_exclude >= 0 ? count - 1 : count);
                return _exclude >= 0 && _pick >= _exclude ? _pick + 1 : _pick;
            }
            float _r = (float)(rng.NextDouble() * _total);
            int _chosen = -1;
            for (int _i = 0; _i < count; _i++) {
                if (_i == _exclude || weights[_i] <= 0f) continue;
                _chosen = _i;
                _r -= weights[_i];
                if (_r < 0f) break;
            }
            return _chosen;
        }

        private int NextShuffle() {
            if (bagPos >= count) {
                for (int _i = 0; _i < count; _i++) bag[_i] = _i;
                for (int _i = count - 1; _i > 0; _i--) {
                    int _j = rng.Next(_i + 1);
                    (bag[_i], bag[_j]) = (bag[_j], bag[_i]);
                }
                // Avoid repeating the last clip of the previous bag at the start of the new one.
                if (bag[0] == last) (bag[0], bag[count - 1]) = (bag[count - 1], bag[0]);
                bagPos = 0;
            }
            return bag[bagPos++];
        }
    }
}
