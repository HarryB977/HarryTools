using UnityEngine;

namespace HarryTools.Audio {
    /// <summary> The transform sounds are heard from: the one set explicitly, else the active AudioListener (searched at most once per second). </summary>
    internal sealed class AudioListenerTracker {
        // ==================== VARIABLES ===================
        private const float SearchInterval = 1f;

        private Transform explicitListener;
        private AudioListener cached;
        private float nextSearch = float.NegativeInfinity;

        // ==================== PROPERTIES ===================
        public Transform Listener {
            get {
                if (explicitListener != null) return explicitListener;
                if (IsUsable(cached)) return cached.transform;

                float _now = Time.realtimeSinceStartup;
                if (_now >= nextSearch) {
                    nextSearch = _now + SearchInterval;
                    cached = Object.FindAnyObjectByType<AudioListener>();
                }
                return IsUsable(cached) ? cached.transform : null;
            }
        }

        // ==================== PUBLIC ===================
        /// <summary> Overrides the listener. Null goes back to the active AudioListener. </summary>
        public void Set(Transform _listener) => explicitListener = _listener;

        // ==================== PRIVATE ===================
        private static bool IsUsable(AudioListener _listener) => _listener != null && _listener.isActiveAndEnabled;
    }
}
