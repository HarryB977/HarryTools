using UnityEngine;

namespace HarryTools.Platformer3DController.Sample {
    [RequireComponent(typeof(Rigidbody))]
    public class Player : MonoBehaviour, IMovementHandler {
        // ==================== VARIABLES ===================
        #region Private Variables
        [Header("Movement")]
        [SerializeField] private MovementController.Data movementData;

        [Header("Camera")]
        [SerializeField] private Transform orientation;

        // References
        private Rigidbody rB;

        // Inputs
        private float lastJumpPressTime = float.NegativeInfinity;
        #endregion

        #region Properties
        public MovementController Movement { get; private set; }
        #endregion

        // ==================== START ====================
        private void Awake() {
            // References
            TryGetComponent(out rB);
            if (orientation == null && Camera.main != null) orientation = Camera.main.transform;

            // Controllers
            Movement = new MovementController(this);
        }

        private void Update() {
            // Inputs
            if (SampleInput.JumpPressed) lastJumpPressTime = Time.time;
        }

        private void FixedUpdate() {
            // Controllers
            Movement.Ticks(Time.fixedDeltaTime);
        }

        private void OnDrawGizmosSelected() {
            // Controllers
            Movement?.ShowGizmos();
        }

        // ==================== INTERFACES ====================
        #region IMovementHandler
        // Inputs
        Vector2 IMovementHandler.MovementInput => SampleInput.Movement;
        bool IMovementHandler.SprintHeld => SampleInput.SprintHeld;
        float IMovementHandler.LastJumpPressTime => lastJumpPressTime;

        // References
        Transform IMovementHandler.Orientation => orientation;
        Rigidbody IMovementHandler.Rigidbody => rB;

        // Data
        MovementController.Data IMovementHandler.Data => movementData;
        #endregion
    }
}
