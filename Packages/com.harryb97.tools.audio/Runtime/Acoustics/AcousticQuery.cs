using UnityEngine;

namespace HarryTools.Audio {
    /// <summary> One voice to simulate. The source position is the logical one (follow target or At), never the perceived one, as of the previous voice update (the simulator runs before the voices move). </summary>
    public readonly struct AcousticQuery {
        // ==================== CONSTRUCTOR ===================
        public AcousticQuery(AudioHandle _handle, Vector3 _sourcePosition, Vector3 _listenerPosition, AudioAsset _asset) {
            Handle = _handle;
            SourcePosition = _sourcePosition;
            ListenerPosition = _listenerPosition;
            Asset = _asset;
        }

        // ==================== PROPERTIES ===================
        public AudioHandle Handle { get; }
        public Vector3 SourcePosition { get; }
        public Vector3 ListenerPosition { get; }
        public AudioAsset Asset { get; }
    }
}
