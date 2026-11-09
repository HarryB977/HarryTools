using UnityEngine;

namespace HarryTools.Audio.Acoustics {
    /// <summary> Runs the acoustic simulation with a profile while this component is enabled. </summary>
    public sealed class AcousticsBootstrap : MonoBehaviour {
        // ==================== VARIABLES ===================
        [Tooltip("Profile to simulate with while this component is enabled.")]
        public AcousticProfile Profile;

        // ==================== LIFECYCLE ===================
        private void OnEnable() {
            if (Profile == null) {
                HarryTools.LoggerTool.Logger.LogWarning("Audio", "AcousticsBootstrap '" + name + "' has no profile.");
                return;
            }
            Acoustics.Use(Profile);
        }

        private void OnDisable() {
            if (Profile != null && Acoustics.Active == Profile) Acoustics.Stop();
        }
    }
}
