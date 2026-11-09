using UnityEngine;

namespace HarryTools.Audio.Acoustics {
    /// <summary> Rays the acoustic modules may launch in one frame, shared between them. Refills once per frame. </summary>
    internal sealed class RayBudget {
        // ==================== VARIABLES ===================
        private int limit;
        private int consumed;
        private int frame = -1;

        // ==================== PROPERTIES ===================
        /// <summary> Rays still available this frame. </summary>
        public int Remaining {
            get {
                Refill();
                return Mathf.Max(0, limit - consumed);
            }
        }

        // ==================== INITIALIZATION ===================
        public RayBudget(int _perFrame) {
            SetLimit(_perFrame);
        }

        /// <summary> Changes the rays per frame. Rays already consumed this frame still count. </summary>
        public void SetLimit(int _perFrame) {
            limit = Mathf.Max(0, _perFrame);
        }

        // ==================== PUBLIC API ===================
        /// <summary> Takes up to <paramref name="_rays"/> rays from this frame and returns how many were granted. </summary>
        public int TryConsume(int _rays) {
            int _granted = Mathf.Clamp(_rays, 0, Remaining);
            consumed += _granted;
            return _granted;
        }

        // ==================== HELPERS ===================
        private void Refill() {
            int _now = Time.frameCount;
            if (_now == frame) return;
            frame = _now;
            consumed = 0;
        }
    }
}
