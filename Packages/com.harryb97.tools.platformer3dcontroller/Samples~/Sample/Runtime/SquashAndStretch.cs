using UnityEngine;

namespace HarryTools.Platformer3DController.Sample {
    /// <summary> Stretches the visual model with the vertical speed and squashes it on landing, keeping its volume. </summary>
    public class SquashAndStretch : MonoBehaviour {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private Player player;
        [SerializeField] private Transform model;

        [Header("Stretch")]
        [SerializeField] private float stretchPerSpeed = 0.015f;
        [SerializeField] private float maxStretch = 0.3f;

        [Header("Landing")]
        [SerializeField] private float landSquash = 0.25f;
        [SerializeField] private float stiffness = 300f;
        [SerializeField] private float damper = 12f;

        private MovementController movement;
        private Vector3 baseScale;
        private float squash;
        private float squashVelocity;
        #endregion

        // ==================== START ====================
        private void Start() {
            movement = player.Movement;
            baseScale = model.localScale;

            movement.OnGrounded += OnGrounded;
        }

        private void OnDestroy() {
            if (movement != null) movement.OnGrounded -= OnGrounded;
        }

        private void LateUpdate() {
            // Landing spring
            float _deltaTime = Time.deltaTime;
            squashVelocity += (-squash * stiffness - squashVelocity * damper) * _deltaTime;
            squash += squashVelocity * _deltaTime;

            // Stretch only in the air, the levitation spring already bounces on the ground
            float _upSpeed = movement.IsLevitating ? 0f : Mathf.Abs(Vector3.Dot(movement.Velocity, movement.Up));
            float _stretch = Mathf.Min(_upSpeed * stretchPerSpeed, maxStretch) - squash;

            // Same volume: the width shrinks as the height grows
            float _height = Mathf.Max(0.1f, 1f + _stretch);
            float _width = 1f / Mathf.Sqrt(_height);
            model.localScale = Vector3.Scale(baseScale, new Vector3(_width, _height, _width));
        }

        // ==================== METHODS ====================
        private void OnGrounded() => squash = landSquash;
    }
}
