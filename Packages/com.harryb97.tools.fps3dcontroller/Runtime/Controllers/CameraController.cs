using System;
using UnityEngine;

namespace HarryTools.FPS3DController {
    public class CameraController {
        [Serializable]
        public class Data {
            [Header("Look")]
            public float mouseSensitivity = 0.1f;
            public float verticalMaxAngle = 89f;
            public float crouchHeight = 0.8f;

            [Header("FOV")]
            public float defaultFov = 80f;
            public float maxSpeedFovBoost = 10f;
            public float fovChangeSpeed = 8f;

            [Header("Camera Bob")]
            public float bobbingAmount = 0.05f;
            public float walkBobSpeed = 14f;
            public float bobFadeSpeed = 6f;

            [Header("Height")]
            public float heightChangeSpeed = 10f;

            [Header("Roll")]
            public float strafeRollAngle = 2f;
            public float rollReturnSpeed = 8f;

            [Header("Slide")]
            public float slideTiltAngle = 5f;
        }

        // ==================== VARIABLES ===================
        #region Private Variables
        // Setup
        private ICameraHandler Handler { get; set; }

        // Look
        private float yaw, pitch, roll, tilt;

        // Bob
        private float defaultHeight = 0f;
        private float baseHeight = 0f;
        private float bobTimer;
        private float bobWeight;
        #endregion

        #region Properties
        private Data Settings => Handler.Data;
        private MovementController Movement => Handler.Movement;
        private float CurrentCameraHeight => Movement.IsCrouching || Movement.IsSliding ? Settings.crouchHeight : defaultHeight;
        #endregion

        // ==================== START ====================
        public CameraController(ICameraHandler _handler) {
            // Setup
            Handler = _handler;

            // References
            defaultHeight = Handler.Head.localPosition.y;
            baseHeight = defaultHeight;
            yaw = Handler.Head.eulerAngles.y;

            // Cursor
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void Ticks(float _deltaTime) {
            UpdateLook();
            UpdateFov(_deltaTime);
            UpdateRoll(_deltaTime);
            HeadBob(_deltaTime);

            Handler.Head.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Handler.Camera.transform.localRotation = Quaternion.Euler(pitch - tilt, 0f, roll);
        }

        /// <summary> Adds to the look angles without returning, e.g. weapon recoil. Positive pitch looks up. </summary>
        public void AddLookOffset(float _pitchUp, float _yaw) {
            yaw += _yaw;
            pitch = Mathf.Clamp(pitch - _pitchUp, -Settings.verticalMaxAngle, Settings.verticalMaxAngle);
        }

        // ==================== METHODS ====================
        private void UpdateLook() {
            // Mouse delta is already per frame: scaling it by deltaTime would tie sensitivity to FPS and time scale
            yaw += Handler.LookInput.x * Settings.mouseSensitivity;
            pitch -= Handler.LookInput.y * Settings.mouseSensitivity;
            pitch = Mathf.Clamp(pitch, -Settings.verticalMaxAngle, Settings.verticalMaxAngle);
        }

        private void UpdateFov(float _deltaTime) {
            float _maxSpeed = Mathf.Max(0.01f, Movement.MaxSpeed);
            float _overSpeed = Mathf.Clamp01((Movement.HorizontalSpeed - _maxSpeed) / _maxSpeed);
            float _targetFov = Settings.defaultFov + Settings.maxSpeedFovBoost * _overSpeed;

            Handler.Camera.fieldOfView = Mathf.Lerp(Handler.Camera.fieldOfView, _targetFov, Settings.fovChangeSpeed * _deltaTime);
        }

        private void UpdateRoll(float _deltaTime) {
            float _targetRoll = Movement.IsGrounded && !Movement.IsSliding ? Handler.MovementInput.x * -Settings.strafeRollAngle : 0f;
            float _targetTilt = Movement.IsSliding ? Settings.slideTiltAngle : 0f;

            roll = Mathf.Lerp(roll, _targetRoll, Settings.rollReturnSpeed * _deltaTime);
            tilt = Mathf.Lerp(tilt, _targetTilt, Settings.rollReturnSpeed * _deltaTime);
        }

        private void HeadBob(float _deltaTime) {
            // Bob strength follows speed and fades in and out, so stopping or landing never cuts it off
            bool _canBob = Movement.IsGrounded && !Movement.IsSliding && !Movement.IsCrouching;
            float _targetWeight = _canBob ? Mathf.Clamp01(Movement.HorizontalSpeed / Mathf.Max(0.01f, Movement.MaxSpeed)) : 0f;
            bobWeight = Mathf.MoveTowards(bobWeight, _targetWeight, Settings.bobFadeSpeed * _deltaTime);

            if (bobWeight > 0f) bobTimer += _deltaTime * Settings.walkBobSpeed;
            else bobTimer = 0f;

            // The base eases between standing and crouched heights independently of the bob
            baseHeight = Mathf.Lerp(baseHeight, CurrentCameraHeight, Settings.heightChangeSpeed * _deltaTime);

            Vector3 _position = Handler.Head.localPosition;
            _position.y = baseHeight + Mathf.Sin(bobTimer) * Settings.bobbingAmount * bobWeight;
            Handler.Head.localPosition = _position;
        }
    }
}
