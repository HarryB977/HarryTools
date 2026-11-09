using UnityEngine;

namespace HarryTools.Audio.Acoustics {
    /// <summary> A surface that a voice's center ray crossed. </summary>
    internal struct DebugHit {
        public Vector3 Point;
        public AcousticMaterial Material;
    }

    /// <summary> Start point of an occlusion ray and whether it reached the listener. </summary>
    internal struct DebugRay {
        public Vector3 From;
        public bool Open;
    }

    /// <summary> What the module traced for one voice in its last finished batch. </summary>
    internal struct DebugVoice {
        public bool Valid;
        public Vector3 Source;
        public Vector3 Listener;
        public float Radius;
        public bool Blocked;
        public AcousticResult Result;
        public int HitCount;
        public DebugHit[] Hits;
        public int RayCount;
        public DebugRay[] Rays;
    }

    /// <summary> Scene view gizmos for the acoustic simulation. Drawn by <see cref="AcousticsBootstrap"/> when its debug toggle is on. </summary>
    internal static class AcousticsDebug {
        // ==================== VARIABLES ===================
        private static readonly Color Free = new Color(0.25f, 0.9f, 0.35f, 1f);
        private static readonly Color Blocked = new Color(0.95f, 0.25f, 0.2f, 1f);
        private static readonly Color Hit = new Color(1f, 0.85f, 0.2f, 1f);

        // ==================== DRAWING ===================
        internal static void Draw(PropagationModule _module) {
            if (_module == null) return;

            DebugVoice[] _voices = _module.DebugVoices;
            Color _previous = Gizmos.color;
            for (int i = 0; i < _voices.Length; i++) {
                if (_voices[i].Valid && _module.IsLive(i)) DrawVoice(_voices[i]);
            }
            Gizmos.color = _previous;
        }

        private static void DrawVoice(in DebugVoice _voice) {
            Gizmos.color = _voice.Blocked ? Blocked : Free;
            Gizmos.DrawLine(_voice.Source, _voice.Listener);
            Gizmos.DrawWireSphere(_voice.Source, _voice.Radius);

            for (int i = 0; i < _voice.RayCount; i++) {
                Gizmos.color = _voice.Rays[i].Open ? Free : Blocked;
                Gizmos.DrawLine(_voice.Rays[i].From, _voice.Listener);
            }

            Gizmos.color = Hit;
            for (int i = 0; i < _voice.HitCount; i++) {
                Gizmos.DrawSphere(_voice.Hits[i].Point, 0.08f);
#if UNITY_EDITOR
                string _name = _voice.Hits[i].Material != null ? _voice.Hits[i].Material.name : "no material";
                UnityEditor.Handles.Label(_voice.Hits[i].Point + Vector3.up * 0.2f, _name);
#endif
            }

#if UNITY_EDITOR
            AcousticResult _result = _voice.Result;
            string _text = "volume " + _result.VolumeMultiplier.ToString("0.00") + "\nlow-pass " + _result.LowPassCutoff.ToString("0") + " Hz\nhigh-pass " + _result.HighPassCutoff.ToString("0") + " Hz";
            UnityEditor.Handles.Label(_voice.Source + Vector3.up * (_voice.Radius + 0.4f), _text);
#endif
        }
    }
}
