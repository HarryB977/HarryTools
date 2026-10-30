using System;
using UnityEngine;

namespace HarryTools.FPS3DController {
    public class MovementController {
        [Serializable]
        public class Data {
            [Header("General")]
            public float gravity = 9.81f;

            [Header("Sprint")]
            public bool canJump = true;
            public float jumpForce = 3f;
            
            [Header("Sprint")]
            public float walkSpeed = 10f;
            [Space]
            public bool canSprint = true;
            public float sprintSpeed = 15f;

            [Header("Crouch")]
            public bool canCrouch = true;
            public float crouchHeight = 1f;
            public float crouchSpeed = 6f;

            [Header("Slide")]
            public bool canSlide = true;
            public float slideDuration = 0.75f;
            public float slideSpeed = 10f;

            [Header("Coyote Time")]
            public bool coyoteTimerEnabled = true;
            public float coyoteTimerDuration = 0.25f;
            
            [Header("Check Ground")]
            public float groundCheckRadius;
            public Vector3 groundCheckOffset;
            public LayerMask groundCheckMask;
        }
        
        // ==================== VARIABLES ===================
        #region Public Variables
        public bool IsGrounded { get; private set; } = false;
        public bool IsSprinting { get; private set; } = false;
        public bool IsCrouching { get; private set; } = false;
        public bool IsSliding { get; private set; } = false;

        public bool BelowCeiling { get; private set; } = false;
        #endregion

        #region Private Variables
        // Setup
        private IMovementHandler Handler { get; set; }

        // Movement
        private Vector3 moveDirection = Vector3.zero;
        private Vector3 slideDirection = Vector3.zero;
        
        private float currentSlideSpeed = 0f;

        // Timers
        private float postSlideCrouchTimer = 0f;
        private float slideTimer = 0f;
        private float coyoteTimer = 0;

        // Collider
        private float defaultHeight = 0f;
        #endregion

        // ==================== START ====================
        public MovementController(IMovementHandler _handler) {
            // Setup
            Handler = _handler;

            // Collider
            defaultHeight = Handler.Collider.height;
        }

        public void Ticks() {
            if (Handler == null) return;

            // Checks
            CheckGround();
            CheckCeiling();
            ChecksCrouch();
            CheckSlide();
            CheckSprint();

            // Movement
            Movement();
        }

        public void ShowGizmos() {
            if (Handler == null) return;

            // Ground Check
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(Handler.Transform.position + Handler.Data.groundCheckOffset, Handler.Data.groundCheckRadius);
        }

        // ==================== METHODS ====================
        #region Checks
        private void CheckGround() {
            IsGrounded = Physics.CheckSphere(
                Handler.Data.groundCheckOffset + Handler.Transform.position,
                Handler.Data.groundCheckRadius,
                Handler.Data.groundCheckMask
            );
        }

        private void CheckCeiling() {
            Vector3 _point1 = Handler.Transform.position + Handler.Collider.center - Vector3.up * (Handler.Collider.height * 0.5f);
            Vector3 _point2 = _point1 + 0.6f * Handler.Collider.height * Vector3.up;

            float _colliderRadius = Handler.Collider.radius * 0.95f;
            float _castDistance = IsSliding ? defaultHeight + 0.2f : defaultHeight - Handler.Data.crouchHeight + 0.2f;

            BelowCeiling = Physics.CapsuleCast(_point1, _point2, _colliderRadius, Vector3.up, _castDistance, Handler.Data.groundCheckMask);
        }

        private void ChecksCrouch() {
            bool _crouch = Handler.Data.canCrouch && Handler.CrouchInput && !IsSliding;

            if (IsSliding) {
                postSlideCrouchTimer = 0.1f;
            }

            if (postSlideCrouchTimer <= 0f) IsCrouching = Handler.Data.canCrouch && (_crouch || (BelowCeiling && !IsSliding));
            else {
                postSlideCrouchTimer -= Time.deltaTime;
                IsCrouching = Handler.Data.canCrouch;
            }

            float _targetHeight = IsCrouching || IsSliding ? Handler.Data.crouchHeight : defaultHeight;
            Handler.Collider.height = Mathf.Lerp(Handler.Collider.height, _targetHeight, Time.deltaTime * 15f);
            Handler.Collider.center = new Vector3(0f, Handler.Collider.height * 0.5f, 0f);
        }

        private void CheckSlide() {
            if (Handler.Data.canSlide && IsSprinting && Handler.CrouchInput && IsGrounded) {
                IsSliding = true;
                slideTimer = Handler.Data.slideDuration;
                slideDirection = Handler.MovementInput.magnitude <= 0.1f ?
                    Handler.Transform.forward :
                    (Handler.Transform.right * Handler.MovementInput.x + Handler.Transform.forward * Handler.MovementInput.y).normalized;
            }

            if (IsSliding) {
                slideTimer -= Time.deltaTime;
                if (slideTimer <= 0f || !IsGrounded) IsSliding = false;

                float _slideProgress = slideTimer / Handler.Data.slideDuration;
                currentSlideSpeed = Handler.Data.slideSpeed * Mathf.Lerp(0.5f, 1f, _slideProgress * _slideProgress);
            }
        }

        private void CheckSprint() {
            IsSprinting = Handler.Data.canSprint && Handler.SprintInput && Handler.MovementInput.y > 0.1f && IsGrounded && !IsCrouching && !IsSliding;            
        }
        #endregion

        #region Movement
        private void Movement() {
            // Coyote Timer
            if (IsGrounded && moveDirection.y < 0) {
                moveDirection.y = -2f;
                coyoteTimer = Handler.Data.coyoteTimerEnabled ? Handler.Data.coyoteTimerDuration : 0f;

            } else if (Handler.Data.coyoteTimerEnabled) {
                coyoteTimer -= Time.deltaTime;
            }

            // Slide
            if (IsSliding) {
                Handler.Rigidbody.linearVelocity += currentSlideSpeed * Time.deltaTime * slideDirection;
            }

            // Speed & Direction
            float _currentSpeed = IsCrouching ? Handler.Data.crouchSpeed : (IsSprinting ? Handler.Data.sprintSpeed : Handler.Data.walkSpeed);
            Vector3 _direction = new(Handler.MovementInput.x, 0f, Handler.MovementInput.y);
            Vector3 _moveVector = Handler.Transform.TransformDirection(_direction) * _currentSpeed;
            _moveVector = Vector3.ClampMagnitude(_moveVector, _currentSpeed);

            // Jump
            if (!IsGrounded && coyoteTimer <= 0f) moveDirection.y -= Handler.Data.gravity * Time.deltaTime;
            else {
                if (Handler.Data.canJump && Handler.JumpInput && !IsSliding) moveDirection.y = Handler.Data.jumpForce;
                else if (moveDirection.y < 0) moveDirection.y = -2f;

            }

            if (!IsSliding) {
                moveDirection = new(_moveVector.x, moveDirection.y, _moveVector.z);
                Handler.Rigidbody.linearVelocity = moveDirection;
            }
        }
        #endregion
    }
}