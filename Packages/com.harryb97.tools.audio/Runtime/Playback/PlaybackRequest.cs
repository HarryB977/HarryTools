using UnityEngine;

namespace HarryTools.Audio {
    /// <summary> What to play and where. Passed by reference to the voice manager. </summary>
    internal readonly struct PlaybackRequest {
        // ==================== VARIABLES ===================
        public readonly AudioAsset Asset;
        public readonly Vector3 Position;
        /// <summary> Transform to follow. Wins over Position while it exists. </summary>
        public readonly Transform Follow;
        /// <summary> Emitter the sound comes from, or null. </summary>
        public readonly AudioEmitter Emitter;
        /// <summary> Effect applied to the voice, or null. </summary>
        public readonly AudioEffectPreset Effect;
        /// <summary> Plays fully 2D whatever the spatial settings of the asset (voice lines without emitter). </summary>
        public readonly bool Force2D;
        /// <summary> Takes and prepares the voice without starting it (queued voice lines). VoiceManager.Activate starts it. </summary>
        public readonly bool Reserve;

        // ==================== CONSTRUCTORS ===================
        public PlaybackRequest(AudioAsset _asset, Vector3 _position, Transform _follow = null) {
            Asset = _asset;
            Position = _position;
            Follow = _follow;
            Emitter = null;
            Effect = null;
            Force2D = false;
            Reserve = false;
        }

        public PlaybackRequest(AudioAsset _asset, Vector3 _position, Transform _follow, AudioEmitter _emitter, AudioEffectPreset _effect, bool _force2D, bool _reserve) {
            Asset = _asset;
            Position = _position;
            Follow = _follow;
            Emitter = _emitter;
            Effect = _effect;
            Force2D = _force2D;
            Reserve = _reserve;
        }

        // ==================== PROPERTIES ===================
        /// <summary> Start position: the follow target if any, else Position. </summary>
        public Vector3 StartPosition => Follow != null ? Follow.position : Position;
    }
}
