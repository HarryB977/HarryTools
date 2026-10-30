using UnityEngine;

namespace HarryTools.Platformer3DController.Sample {
    /// <summary> Dust particles on jump, landing and footsteps. On a MovingPlatform the particles move with it. </summary>
    public class PlayerDust : MonoBehaviour {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private Player player;
        [SerializeField] private ParticleSystem dust;

        [Header("Bursts")]
        [SerializeField] private int jumpBurst = 3;
        [SerializeField] private int landBurst = 7;

        [Header("Footsteps")]
        [Tooltip("Ground distance travelled between footsteps")]
        [SerializeField] private float footstepDistance = 2f;
        [SerializeField] private int footstepBurst = 1;

        private ParticleSystem.MainModule main;
        private MovementController movement;
        private float travelled;
        #endregion

        // ==================== START ====================
        private void Start() {
            main = dust.main;
            movement = player.Movement;

            movement.OnJump += OnJump;
            movement.OnGrounded += OnGrounded;
        }

        private void OnDestroy() {
            if (movement == null) return;

            movement.OnJump -= OnJump;
            movement.OnGrounded -= OnGrounded;
        }

        private void LateUpdate() {
            // Emitter
            dust.transform.position = movement.Ground.Point;
            UpdateSimulationSpace();

            // Footsteps
            if (!movement.IsLevitating) {
                travelled = 0f;
                return;
            }

            travelled += movement.HorizontalSpeed * Time.deltaTime;
            if (travelled < footstepDistance) return;

            travelled = 0f;
            dust.Emit(footstepBurst);
        }

        // ==================== METHODS ====================
        private void OnJump() => dust.Emit(jumpBurst);
        private void OnGrounded() => dust.Emit(landBurst);

        private void UpdateSimulationSpace() {
            Transform _platform = movement.Platform != null ? movement.Platform.transform : null;
            if (main.simulationSpace == ParticleSystemSimulationSpace.Custom && main.customSimulationSpace == _platform) return;
            if (main.simulationSpace == ParticleSystemSimulationSpace.World && _platform == null) return;

            main.simulationSpace = _platform != null ? ParticleSystemSimulationSpace.Custom : ParticleSystemSimulationSpace.World;
            main.customSimulationSpace = _platform;
        }
    }
}
