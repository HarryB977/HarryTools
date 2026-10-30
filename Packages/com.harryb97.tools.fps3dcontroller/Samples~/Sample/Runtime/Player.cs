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
            // Controllers
            movementController.Ticks();
            cameraController.Ticks();
        }

        private void OnDrawGizmos() {
            // Controllers
            movementController?.ShowGizmos();
        }

        // ==================== INTERFACES ====================
        #region IMovementHandler
        // Inputs
        Vector2 IMovementHandler.MovementInput => new(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));

        bool IMovementHandler.JumpInput => Input.GetKey(KeyCode.Space);
        bool IMovementHandler.CrouchInput => Input.GetKey(KeyCode.LeftControl);
        bool IMovementHandler.SprintInput => Input.GetKey(KeyCode.LeftShift);

        // References
        Transform IMovementHandler.Transform => transform;
        Rigidbody IMovementHandler.Rigidbody => rB;
        CapsuleCollider IMovementHandler.Collider => col;

        // Data
        MovementController.Data IMovementHandler.Data => movementData;

        #endregion

        #region ICameraHandler
        // Inputs
        Vector2 ICameraHandler.MovementInput => new(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        Vector2 ICameraHandler.LookInput => new(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));

        // References
        Transform ICameraHandler.Transform => transform;
        Rigidbody ICameraHandler.Rigidbody => rB;
        Transform ICameraHandler.Head => head;
        Camera ICameraHandler.Camera => headCamera;

        MovementController ICameraHandler.Movement => movementController;

        // Data
        CameraController.Data ICameraHandler.Data => cameraData;
         #endregion
    }
}