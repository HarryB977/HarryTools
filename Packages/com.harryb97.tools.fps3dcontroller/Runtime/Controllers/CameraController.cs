using System;
using UnityEngine;

namespace HarryTools.FPS3DController {
    public class CameraController {
        [Serializable]
        public class Data {
            [Header("Settings")]
            public float mouseSensitivity = 25f;
            public float verticalMaxAngle = 90f;
            public float snap = 100;
            public float crouchHeight = 0.5f;

            [Header("FOV")]
            public float defaultFov = 60f;
            public float sprintFov = 70f;
            public float fovChangeSpeed = 5f;

            [Header("Camera Bob")]
            public float bobbingAmount = 0.05f;
            [Space]
            public float walkBobSpeed = 14f;
            public float sprintBobMultiplier = 1.2f;
            public float recoilReturnSpeed = 8f;

            [Header("Slide")]
            public float slideTiltAngle = 5f;
            public float slideFovBoost = 5f;
        }

        // ==================== VARIABLES ===================
        #region Properties
        private float CurrentCameraHeight => Handler.Movement.IsCrouching || Handler.Movement.IsSliding ? Handler.Data.crouchHeight : defaultHeight;
        #endregion

        #region Private Variables
        // Setup
        private ICameraHandler Handler { get; set; }

        // References
        private float defaultHeight = 0f;
        private Vector3 recoil = Vector3.zero;

        // Look
        private float rotationX, rotationY;
        private float xVelocity, yVelocity;

        // Bob
        private float bobTimer;
        #endregion

        // ==================== START ====================
        public CameraController(ICameraHandler _handler) {
            // Setup
            Handler = _handler;

            // References
            defaultHeight = Handler.Head.localPosition.y;

            // Cursor
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void Ticks() {
            UpdateLook();
            UpdateFov();

            HeadBob();
        }

        // ==================== METHODS ====================
        private void UpdateLook() {
            float _mouseX = Handler.LookInput.x * 10f * Handler.Data.mouseSensitivity * Time.deltaTime;
            float _mouseY = Handler.LookInput.y * 10f * Handler.Data.mouseSensitivity * Time.deltaTime;

            rotationX += _mouseX;
            rotationY -= _mouseY;
            rotationY = Mathf.Clamp(rotationY, -Handler.Data.verticalMaxAngle, Handler.Data.verticalMaxAngle);

            xVelocity = Mathf.Lerp(xVelocity, rotationX, Handler.Data.snap * Time.deltaTime);
            yVelocity = Mathf.Lerp(yVelocity, rotationY, Handler.Data.snap * Time.deltaTime);

            if (Handler.Movement.IsSliding) {
                Handler.Camera.transform.localRotation = Quaternion.Lerp(
                    Handler.Camera.transform.localRotation,
                    Quaternion.Euler(yVelocity - Handler.Data.slideTiltAngle, 0f, 0f),
                    Time.deltaTime * 10f
                );

            } else {
                Handler.Camera.transform.localRotation = Quaternion.Euler(yVelocity, 0f, 0f);
            }

            Handler.Transform.rotation = Quaternion.Euler(0f, rotationX, 0f);
        }

        private void UpdateFov() {
            float _targetFov = Handler.Movement.IsSprinting ?
                Handler.Data.sprintFov : (
                    Handler.Movement.IsSliding ?
                        Handler.Data.sprintFov + Handler.Data.slideFovBoost :
                        Handler.Data.defaultFov
                );

            Handler.Camera.fieldOfView = Mathf.Lerp(
                Handler.Camera.fieldOfView,
                _targetFov,
                Time.deltaTime * Handler.Data.fovChangeSpeed
            );
        }

        private void HeadBob() {
            if (!Handler.Movement.IsGrounded || Handler.Movement.IsSliding || Handler.Movement.IsCrouching) {
                ResetBob();

                Handler.Head.localRotation = Quaternion.RotateTowards(
                    Handler.Head.localRotation,
                    Quaternion.Euler(recoil),
                    Handler.Data.recoilReturnSpeed * Time.deltaTime
                );

                return;
            }

            if (Mathf.Abs(Handler.Rigidbody.linearVelocity.x) <= 0.1f && Mathf.Abs(Handler.Rigidbody.linearVelocity.z) <= 0.1f) ResetBob();
            else {
                float _bobSpeed = Handler.Data.walkBobSpeed * (Handler.Movement.IsSprinting ? Handler.Data.sprintBobMultiplier : 1f);
                bobTimer += Time.deltaTime * _bobSpeed;

                Handler.Head.localPosition = new Vector3(
                    Handler.Head.localPosition.x,
                    CurrentCameraHeight + Mathf.Sin(bobTimer) * Handler.Data.bobbingAmount,
                    Handler.Head.localPosition.z
                );

                recoil.z = Handler.MovementInput.x * -2f;
            }

            Handler.Head.localRotation = Quaternion.RotateTowards(
                Handler.Head.localRotation,
                Quaternion.Euler(recoil),
                Handler.Data.recoilReturnSpeed * Time.deltaTime
            );

            void ResetBob() {
                bobTimer = 0f;
                Handler.Head.localPosition = new Vector3(
                    Handler.Head.localPosition.x,
                    Mathf.Lerp(Handler.Head.localPosition.y, CurrentCameraHeight, Time.deltaTime * Handler.Data.walkBobSpeed),
                    Handler.Head.localPosition.z
                );

                recoil = Vector3.zero;
            }
        }
    }
}