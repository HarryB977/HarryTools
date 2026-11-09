using UnityEngine;

namespace HarryTools.Audio.Acoustics {
    /// <summary> Runs the acoustic simulation with a profile while this component is enabled. </summary>
    public sealed class AcousticsBootstrap : MonoBehaviour {
        // ==================== VARIABLES ===================
        [Tooltip("Profile to simulate with while this component is enabled.")]
        public AcousticProfile Profile;
        [Tooltip("Draw the occlusion rays, the surfaces they cross and the result of each voice in the Scene view. Costs a few copies per voice.")]
        public bool DebugGizmos;

        // ==================== LIFECYCLE ===================
        private void OnEnable() {
            if (Profile == null) {
                HarryTools.LoggerTool.Logger.LogWarning("Audio", "AcousticsBootstrap '" + name + "' has no profile.");
                return;
            }
            Acoustics.DebugCapture = DebugGizmos;
            Acoustics.Use(Profile);
        }

        private void OnDisable() {
            if (Profile != null && Acoustics.Active == Profile) {
                Acoustics.Stop();
                Acoustics.DebugCapture = false;
            }
        }

        private void OnDrawGizmos() {
            if (!Application.isPlaying || !isActiveAndEnabled) return;
            if (Acoustics.DebugCapture != DebugGizmos) Acoustics.DebugCapture = DebugGizmos;
            if (DebugGizmos) AcousticsDebug.Draw(Acoustics.Module);
        }
    }
}
