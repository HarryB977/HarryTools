using UnityEngine;

namespace HarryTools.Platformer3DController.Sample {
    /// <summary> Third person camera that orbits the target with the mouse and pulls in front of walls. </summary>
    public class OrbitCamera : MonoBehaviour {
        // ==================== VARIABLES ===================
        #region Private Variables
        [Header("Target")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 targetOffset = new(0f, 0.5f, 0f);
        [SerializeField] private float horizontalSmoothTime = 0.05f;
        [Tooltip("Slower vertical follow, so jumps do not shake the camera")]
        [SerializeField] private float verticalSmoothTime = 0.25f;

        [Header("Orbit")]
        [SerializeField] private float sensitivity = 0.15f;
        [SerializeField] private float minPitch = -30f;
        [SerializeField] private float maxPitch = 70f;
        [SerializeField] private float pitch = 20f;

        [Header("Distance")]
        [SerializeField] private float distance = 8f;
        [SerializeField] private float minDistance = 3f;
        [SerializeField] private float maxDistance = 14f;
        [SerializeField] private float zoomStep = 1f;

        [Header("Collision")]
        [SerializeField] private float collisionRadius = 0.25f;
        [SerializeField] private LayerMask collisionMask = 1;

        private Vector3 pivot;
        private Vector3 pivotVelocity;
        private float pivotVerticalVelocity;
        private float yaw;
        #endregion

        // ==================== START ====================
        private void Start() {
            yaw = transform.eulerAngles.y;
            if (target != null) pivot = target.position + targetOffset;

            SetCursorLocked(true);
        }

        private void LateUpdate() {
            if (target == null) return;

            // Cursor
            if (SampleInput.UnlockCursorPressed) SetCursorLocked(false);
            else if (SampleInput.LockCursorPressed) SetCursorLocked(true);

            // Orbit
            if (Cursor.lockState == CursorLockMode.Locked) {
                Vector2 _look = SampleInput.Look * sensitivity;
                yaw += _look.x;
                pitch = Mathf.Clamp(pitch - _look.y, minPitch, maxPitch);
                distance = Mathf.Clamp(distance - SampleInput.Zoom * zoomStep, minDistance, maxDistance);
            }

            // Follow
            Vector3 _goal = target.position + targetOffset;
            Vector3 _horizontal = Vector3.SmoothDamp(new Vector3(pivot.x, 0f, pivot.z), new Vector3(_goal.x, 0f, _goal.z), ref pivotVelocity, horizontalSmoothTime);
            float _vertical = Mathf.SmoothDamp(pivot.y, _goal.y, ref pivotVerticalVelocity, verticalSmoothTime);
            pivot = new Vector3(_horizontal.x, _vertical, _horizontal.z);

            // Collision
            Quaternion _rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 _back = _rotation * Vector3.back;
            float _distance = distance;
            if (Physics.SphereCast(pivot, collisionRadius, _back, out RaycastHit _hit, distance, collisionMask, QueryTriggerInteraction.Ignore)) _distance = _hit.distance;

            transform.SetPositionAndRotation(pivot + _back * _distance, _rotation);
        }

        // ==================== METHODS ====================
        private void SetCursorLocked(bool _locked) {
            Cursor.lockState = _locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !_locked;
        }
    }
}
