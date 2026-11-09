using UnityEngine;

namespace HarryTools.Audio.Acoustics {
    /// <summary> Frequency bands the acoustic model works with. </summary>
    public enum AcousticBand { Low, Mid, High }

    /// <summary> How a surface absorbs and lets sound through, per frequency band. </summary>
    [CreateAssetMenu(menuName = "HarryTools/Audio/Acoustics/Material")]
    public sealed class AcousticMaterial : ScriptableObject {
        // ==================== VARIABLES ===================
        /// <summary> Thickness in meters that <see cref="Transmission"/> is measured for. </summary>
        public const float ReferenceThickness = 0.2f;

        [Tooltip("Fraction of energy absorbed when sound bounces off the surface. X = Low, Y = Mid, Z = High band, each 0 to 1.")]
        public Vector3 Absorption = new Vector3(0.1f, 0.06f, 0.04f);
        [Tooltip("Fraction of energy that passes through 0.2 m of this material. X = Low, Y = Mid, Z = High band, each 0 to 1.")]
        public Vector3 Transmission = new Vector3(0.05f, 0.02f, 0.01f);
    }
}
