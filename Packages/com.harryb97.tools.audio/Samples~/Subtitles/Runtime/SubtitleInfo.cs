using UnityEngine;

namespace HarryTools.Audio.Subtitles {
    /// <summary> One subtitle line being shown. Started, TextChanged and Ended of the same line share the Id. </summary>
    public readonly struct SubtitleInfo {
        // ==================== CONSTRUCTOR ===================
        internal SubtitleInfo(int _id, string _text, SubtitleKind _kind, SpeakerAsset _speaker, string _speakerName, AudioAsset _asset,
                              AudioHandle _handle, float _duration, Vector3 _position, bool _is3D) {
            Id = _id;
            Text = _text;
            Kind = _kind;
            Speaker = _speaker;
            SpeakerName = _speakerName;
            Asset = _asset;
            Handle = _handle;
            Duration = _duration;
            Position = _position;
            Is3D = _is3D;
        }

        // ==================== PROPERTIES ===================
        /// <summary> Unique per emitted line. </summary>
        public int Id { get; }
        /// <summary> Text exactly as it comes out of the table; markup is not interpreted. </summary>
        public string Text { get; }
        public SubtitleKind Kind { get; }
        /// <summary> Speaker of the voice line, or null. </summary>
        public SpeakerAsset Speaker { get; }
        /// <summary> Resolved name of the speaker, or empty. </summary>
        public string SpeakerName { get; }
        public AudioAsset Asset { get; }
        /// <summary> Playback the line belongs to. Already invalid in Ended when the audio ended first. </summary>
        public AudioHandle Handle { get; }
        /// <summary> Seconds the line is expected to last, or -1 when it lasts until a looping sound stops. Rely on Ended to remove it. </summary>
        public float Duration { get; }
        /// <summary> Where the sound is (its logical position). </summary>
        public Vector3 Position { get; }
        public bool Is3D { get; }
    }
}
