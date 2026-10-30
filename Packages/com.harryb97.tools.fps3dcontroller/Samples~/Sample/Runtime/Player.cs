using UnityEngine;

namespace HarryTools.FPS3DController.Sample {
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public class Player : MonoBehaviour, IMovementHandler, ICameraHandler {
        // ==================== VARIABLES ===================
        #region Private Variables
        [Header("Movement")]
        [SerializeField] private MovementController.Data movementData;

        [Header("Camera")]
        [SerializeField] private Transform head;
        [SerializeField] private Camera headCamera;
        [Space]
        [SerializeField] private CameraController.Data cameraData;

        // Controllers
        private MovementController movementController;
        private CameraController cameraController;

        // References
        private Rigidbody rB;
        private CapsuleCollider col;

        // Inputs
        private float lastJumpPressTime = float.NegativeInfinity;
        private float lastDashPressTime = float.NegativeInfinity;
        #endregion

        // ==================== START ====================
        private void Awake() {
            // References
            TryGetComponent(out rB);
            TryGetComponent(out col);
        }

        private void Start() {
            // Controllers
            movementController = new MovementController(this);
            cameraController = new CameraController(this);
        }

        private void Update() {
            // Inputs
            if (Input.GetKeyDown(KeyCode.Space)) lastJumpPressTime = Time.time;
            if (Input.GetKeyDown(KeyCode.LeftShift)) lastDashPressTime = Time.time;

            // Controllers
            cameraController.Ticks(Time.deltaTime);
        }

        private void FixedUpdate() {
            // Controllers
            movementController.Ticks(Time.fixedDeltaTime);
        }

        private void OnDrawGizmos() {
            // Controllers
            movementController?.ShowGizmos();
        }

        // ==================== INTERFACES ====================
        #region IMovementHandler
        // Inputs
        Vector2 IMovementHandler.MovementInput => new(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

        bool IMovementHandler.JumpHeld => Input.GetKey(KeyCode.Space);
        bool IMovementHandler.CrouchHeld => Input.GetKey(KeyCode.LeftControl);

        float IMovementHandler.LastJumpPressTime => lastJumpPressTime;
        float IMovementHandler.LastDashPressTime => lastDashPressTime;

        // References
        Transform IMovementHandler.Transform => transform;
        Transform IMovementHandler.Orientation => head;
        Rigidbody IMovementHandler.Rigidbody => rB;
        CapsuleCollider IMovementHandler.Collider => col;

        // Data
        MovementController.Data IMovementHandler.Data => movementData;
        #endregion

        #region ICameraHandler
        // Inputs
        Vector2 ICameraHandler.MovementInput => new(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        Vector2 ICameraHandler.LookInput => new(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));

        // References
        Transform ICameraHandler.Head => head;
        Camera ICameraHandler.Camera => headCamera;

        MovementController ICameraHandler.Movement => movementController;

        // Data
        CameraController.Data ICameraHandler.Data => cameraData;
        #endregion
    }
}
