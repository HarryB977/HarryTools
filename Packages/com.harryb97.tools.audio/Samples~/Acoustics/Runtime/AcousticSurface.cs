using UnityEngine;

namespace HarryTools.Audio.Acoustics {
    /// <summary> Assigns an acoustic material to the colliders of this object and its children. </summary>
    [DisallowMultipleComponent]
    public sealed class AcousticSurface : MonoBehaviour {
        // ==================== VARIABLES ===================
        [Tooltip("Material applied to every collider under this object that has no closer AcousticSurface.")]
        public AcousticMaterial Material;
    }
}
