using UnityEngine;

namespace HarryTools.Audio.Subtitles {
    /// <summary> Global subtitle settings. </summary>
    [CreateAssetMenu(menuName = "HarryTools/Audio/Subtitle Settings")]
    public sealed class SubtitleSettings : ScriptableObject {
        // ==================== VARIABLES ===================
        [Min(0), Tooltip("Default distance in meters within which a 3D sound shows its subtitles.")]
        public float HearingDistance = 15f;
        [Min(0), Tooltip("Margin to leave the hearing range: subtitles hide at HearingDistance x (1 + margin), so they do not flicker at the edge.")]
        public float ExitMargin = 0.1f;
    }
}
