using UnityEngine;

namespace HarryTools.Audio {
    /// <summary> Reusable spatial settings shared by many audio assets. </summary>
    [CreateAssetMenu(menuName = "HarryTools/Audio/Spatial Profile", fileName = "SpatialProfile")]
    public sealed class SpatialProfile : ScriptableObject {
        // ==================== VARIABLES ===================
        public SpatialSettings Settings = SpatialSettings.Default3D;
    }
}
