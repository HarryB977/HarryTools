using UnityEngine;

namespace HarryTools.Audio {
    /// <summary> What to play and where. Passed by reference to the voice manager. </summary>
    internal readonly struct PlaybackRequest {
        // ==================== VARIABLES ===================
        public readonly AudioAsset Asset;
        public readonly Vector3 Position;
        /// <summary> Transform to follow. Wins over Position while it exists. </summary>
        public readonly Transform Follow;

        // ==================== CONSTRUCTORS ===================
        public PlaybackRequest(AudioAsset _asset, Vector3 _position, Transform _follow = null) {
            Asset = _asset;
            Position = _position;
            Follow = _follow;
        }

        // ==================== PROPERTIES ===================
        /// <summary> Start position: the follow target if any, else Position. </summary>
        public Vector3 StartPosition => Follow != null ? Follow.position : Position;
    }
}
