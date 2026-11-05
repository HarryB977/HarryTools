using UnityEngine;

namespace HarryTools.Audio {
    /// <summary> The transform sounds are heard from: the one set explicitly, else the first enabled AudioListener (searched at most once per second, only while there is no usable one). </summary>
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
                    cached = FindEnabled();
                }
                return IsUsable(cached) ? cached.transform : null;
            }
        }

        // ==================== PUBLIC ===================
        /// <summary> Overrides the listener. Null goes back to the active AudioListener. </summary>
        public void Set(Transform _listener) => explicitListener = _listener;

        // ==================== PRIVATE ===================
        // FindAnyObjectByType can keep returning a listener whose component is disabled, so look for the first enabled one.
        private static AudioListener FindEnabled() {
            AudioListener[] _all = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
            for (int _i = 0; _i < _all.Length; _i++) {
                if (IsUsable(_all[_i])) return _all[_i];
            }
            return null;
        }

        private static bool IsUsable(AudioListener _listener) => _listener != null && _listener.isActiveAndEnabled;
    }
}
