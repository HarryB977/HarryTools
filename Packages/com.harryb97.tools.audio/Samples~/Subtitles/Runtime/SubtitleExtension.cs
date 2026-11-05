using System;
using System.Collections.Generic;
using UnityEngine;

namespace HarryTools.Audio.Subtitles {
    /// <summary> Subtitles of one clip, used instead of the asset entries when that clip plays. </summary>
    [Serializable]
    public sealed class ClipSubtitles {
        // ==================== VARIABLES ===================
        [Tooltip("Clip these entries belong to. It should be one of the clips of the asset.")]
        public AudioClip Clip;
        [Tooltip("Entries used instead of the asset entries when this clip plays.")]
        public List<SubtitleEntry> Entries = new List<SubtitleEntry>();
    }

    /// <summary> Adds subtitles to an audio asset. </summary>
    [Serializable]
    public sealed class SubtitleExtension : IAudioAssetExtension {
        // ==================== VARIABLES ===================
        [Tooltip("Subtitle lines of the asset. Used for every clip that has no override.")]
        public List<SubtitleEntry> Entries = new List<SubtitleEntry>();
        [Tooltip("Per-clip entries for assets whose clips need different text.")]
        public List<ClipSubtitles> ClipOverrides = new List<ClipSubtitles>();
        [Tooltip("Hide the subtitles when the audio is stopped before its end.")]
        public bool EndOnStop = true;
        [Tooltip("Use HearingDistance instead of the one in the Subtitle Settings.")]
        public bool OverrideHearingDistance;
        [Min(0), Tooltip("Distance in meters within which a 3D sound shows its subtitles.")]
        public float HearingDistance;

        // ==================== PUBLIC ===================
        /// <summary> Entries of the override for the clip if there is one, otherwise the asset entries. </summary>
        public IReadOnlyList<SubtitleEntry> GetEntries(AudioClip _clip) {
            if (_clip != null) {
                for (int _i = 0; _i < ClipOverrides.Count; _i++) {
                    ClipSubtitles _override = ClipOverrides[_i];
                    if (_override != null && _override.Clip == _clip) return _override.Entries;
                }
            }
            return Entries;
        }

        /// <summary> Adds the override clips that the owner does not play: not in its Clips and not the Intro of a music asset. </summary>
        public void FindMissingClips(AudioAsset _owner, List<AudioClip> _results) {
            if (_owner == null || _results == null) return;
            AudioClip _intro = (_owner as MusicAsset)?.Intro;
            for (int _i = 0; _i < ClipOverrides.Count; _i++) {
                AudioClip _clip = ClipOverrides[_i]?.Clip;
                if (_clip == null || _clip == _intro || ContainsClip(_owner, _clip)) continue;
                _results.Add(_clip);
            }
        }

        // ==================== PRIVATE ===================
        private static bool ContainsClip(AudioAsset _owner, AudioClip _clip) {
            IReadOnlyList<ClipEntry> _clips = _owner.Clips;
            for (int _i = 0; _i < _clips.Count; _i++) {
                if (_clips[_i] != null && _clips[_i].Clip == _clip) return true;
            }
            return false;
        }
    }
}
