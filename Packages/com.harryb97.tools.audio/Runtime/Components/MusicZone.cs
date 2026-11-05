using System.Collections.Generic;
using HarryTools.DomainReload;
using UnityEngine;

namespace HarryTools.Audio {
    /// <summary>
    /// Plays its music while the listener is inside the collider. Checked every 0.25 s by the audio runtime with
    /// Collider.ClosestPoint, which needs a Box, Sphere, Capsule or convex Mesh collider (a trigger is recommended).
    /// Overlapping zones: the highest Priority wins, then the last one entered. Leaving every zone brings back the music
    /// that was current before the first one (or stops it), even if Audio.Music.Play was called while inside a zone.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    [AddComponentMenu("HarryTools/Audio/Music Zone")]
    [DisallowMultipleComponent]
    public sealed class MusicZone : MonoBehaviour {
        // ==================== VARIABLES ===================
        [ClearOnReload(AssignNewInstance = true)] private static List<MusicZone> registered = new List<MusicZone>();

        [Tooltip("Music played while the listener is inside. Empty plays silence.")]
        public MusicAsset Music;
        [Tooltip("Higher wins when zones overlap. On a tie, the zone entered last wins.")]
        public int Priority;
        [Tooltip("Crossfade in seconds when entering or leaving. Negative uses the music channel default: the asset FadeIn when nothing is sounding, else DefaultCrossfade of the settings; when leaving to silence, the FadeOut of the playing asset.")]
        public float Crossfade = -1f;

        private Collider zoneCollider;

        // ==================== PROPERTIES ===================
        /// <summary> Enabled zones. Read by the music channel. </summary>
        internal static List<MusicZone> Registered => registered;

        // ==================== UNITY ===================
        private void OnEnable() {
            zoneCollider = GetComponent<Collider>();
            if (zoneCollider is MeshCollider _mesh && !_mesh.convex) {
                HarryTools.LoggerTool.Logger.LogWarning("Audio", "MusicZone '" + name + "' needs a convex MeshCollider (Collider.ClosestPoint does not support concave ones).");
            }
            if (!registered.Contains(this)) registered.Add(this);
        }

        private void OnDisable() => registered.Remove(this);

        // ==================== INTERNAL ===================
        /// <summary> True when the point is inside (or on) the collider. </summary>
        internal bool Contains(Vector3 _point) {
            if (zoneCollider == null || !zoneCollider.enabled) return false;
            return (zoneCollider.ClosestPoint(_point) - _point).sqrMagnitude < 1e-6f;
        }
    }
}
