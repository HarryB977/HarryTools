using System;
using UnityEngine;

namespace HarryTools.FPS3DController {
    public class MovementController {
        [Serializable]
        public class Data {
            [Header("General")]
            public float gravity = 15f;

            [Header("Ground")]
            public float maxSpeed = 10f;
            public float groundAcceleration = 10f;
            public float friction = 4f;
            public float stopSpeed = 2f;
            public float maxGroundAngle = 45f;

            [Header("Air")]
            public float airAcceleration = 10f;
            public float airWishSpeedCap = 1f;

            [Header("Jump")]
            public float jumpHeight = 1.2f;
            public int airJumps = 1;
            public float jumpBufferTime = 0.1f;
            public bool autoBunnyhop = false;
            public float coyoteTimeDuration = 0.1f;

            [Header("Dash")]
            public int dashes = 1;
            public float dashSpeed = 18f;

            [Header("Crouch")]
            public float crouchHeight = 1f;
            public float crouchSpeed = 5f;
            public float crouchTransitionSpeed = 15f;

            [Header("Slide")]
            public float slideStartSpeed = 7f;
            public float slideFriction = 0.5f;
            public float slideStopSpeed = 4f;

            [Header("Step")]
            public float stepHeight = 0.35f;

            [Header("Ground Check")]
            public float groundCheckDistance = 0.1f;
            public LayerMask groundCheckMask = 1;
        }

        // ==================== VARIABLES ===================
        #region Public Variables
        public bool IsGrounded { get; private set; } = false;
        public bool IsCrouching { get; private set; } = false;
        public bool IsSliding { get; private set; } = false;
        public bool BelowCeiling { get; private set; } = false;

        public float HorizontalSpeed { get; private set; } = 0f;

        public int AirJumpsLeft { get; private set; } = 0;
        public int DashesLeft { get; private set; } = 0;

        public float MaxSpeed => Settings.maxSpeed;
        #endregion

        #region Private Variables
        // Setup
        private IMovementHandler Handler { get; set; }

        // Movement
        private Vector3 velocity = Vector3.zero;
        private float groundHeight = 0f;
        private float groundSnapVelocity = 0f;

        // Inputs
        private float consumedJumpPressTime = float.NegativeInfinity;
        private float consumedDashPressTime = float.NegativeInfinity;

        // Timers
        private float coyoteTimer = 0f;

        // Collider: heights are the full player height, feet to head
        private float defaultHeight = 0f;
        private float currentHeight = 0f;
        #endregion

        #region Properties
        private Data Settings => Handler.Data;
        private float JumpVelocity => Mathf.Sqrt(2f * Settings.gravity * Settings.jumpHeight);
        private float ColliderBottom => Settings.stepHeight;

        private bool JumpRequested => IsFreshPress(Handler.LastJumpPressTime, consumedJumpPressTime);
        private bool DashRequested => IsFreshPress(Handler.LastDashPressTime, consumedDashPressTime);
        #endregion

        // ==================== START ====================
        public MovementController(IMovementHandler _handler) {
            // Setup
            Handler = _handler;

            // Collider
            defaultHeight = Handler.Collider.height;
            currentHeight = defaultHeight;
            ApplyColliderLayout();

            // Charges
            AirJumpsLeft = Settings.airJumps;
            DashesLeft = Settings.dashes;
        }

        public void Ticks(float _deltaTime) {
            if (Handler == null) return;

            // Start from the physics result so wall and ceiling hits are kept
            velocity = Handler.Rigidbody.linearVelocity;

            // Checks
            CheckGround();
            CheckCeiling();
            UpdateCrouch(_deltaTime);
            UpdateSlide();

            // Actions
            bool _jumped = TryJump();
            bool _dashed = TryDash();

            // Movement
            if (IsGrounded) GroundMove(_deltaTime);
            else AirMove(_deltaTime, !_dashed);

            HorizontalSpeed = new Vector2(velocity.x, velocity.z).magnitude;
            Handler.Rigidbody.linearVelocity = velocity;

            if (!_jumped && !IsGrounded) coyoteTimer -= _deltaTime;
        }

        public void ShowGizmos() {
            if (Handler == null) return;

            // Ground Check
            Gizmos.color = IsGrounded ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(GroundCheckOrigin() + Vector3.down * GroundCheckLength(IsGrounded), GroundCheckRadius());
        }

        // ==================== METHODS ====================
        #region Checks
        private void CheckGround() {
            bool _wasGrounded = IsGrounded;
            bool _hit = Physics.SphereCast(
                GroundCheckOrigin(),
                GroundCheckRadius(),
                Vector3.down,
                out RaycastHit _hitInfo,
                GroundCheckLength(_wasGrounded),
                Settings.groundCheckMask,
                QueryTriggerInteraction.Ignore
            );

            float _height = GroundCheckOrigin().y - _hitInfo.distance - GroundCheckRadius();

            // On a ledge edge the sphere reports a steep normal; a straight ray tells whether there is floor below
            if (_hit && Vector3.Angle(_hitInfo.normal, Vector3.up) > Settings.maxGroundAngle) {
                _hit = Physics.Raycast(
                    GroundCheckOrigin(),
                    Vector3.down,
                    out _hitInfo,
                    GroundCheckLength(_wasGrounded) + GroundCheckRadius(),
                    Settings.groundCheckMask,
                    QueryTriggerInteraction.Ignore
                );
                _height = _hitInfo.point.y;
            }

            // Walls are rejected by angle. Rising faster than the ground motion imposed last tick (a jump) is not grounded;
            // a normal-based check would misread horizontal speed on a ledge edge as leaving the ground
            IsGrounded = _hit
                && Vector3.Angle(_hitInfo.normal, Vector3.up) <= Settings.maxGroundAngle
                && velocity.y - groundSnapVelocity <= 0.1f;

            groundHeight = _height;
            groundSnapVelocity = 0f;

            if (IsGrounded) {
                coyoteTimer = Settings.coyoteTimeDuration;
                AirJumpsLeft = Settings.airJumps;
                DashesLeft = Settings.dashes;
            }
        }

        private void CheckCeiling() {
            float _radius = Handler.Collider.radius * 0.95f;
            Vector3 _top = Handler.Rigidbody.position + Vector3.up * (ColliderBottom + Handler.Collider.height - Handler.Collider.radius);
            float _distance = defaultHeight - currentHeight + 0.05f;

            BelowCeiling = _distance > 0.06f && Physics.SphereCast(
                _top,
                _radius,
                Vector3.up,
                out _,
                _distance,
                Settings.groundCheckMask,
                QueryTriggerInteraction.Ignore
            );
        }

        private bool IsFreshPress(float _pressTime, float _consumedTime) {
            return _pressTime > _consumedTime && Time.time - _pressTime <= Settings.jumpBufferTime;
        }
        #endregion

        #region Crouch & Slide
        private void UpdateCrouch(float _deltaTime) {
            bool _wantsCrouch = Handler.CrouchHeld || IsSliding;
            IsCrouching = _wantsCrouch || (IsCrouching && BelowCeiling);

            float _targetHeight = IsCrouching ? Settings.crouchHeight : defaultHeight;
            float _oldHeight = currentHeight;
            float _newHeight = Mathf.Lerp(_oldHeight, _targetHeight, Settings.crouchTransitionSpeed * _deltaTime);
            if (Mathf.Abs(_newHeight - _targetHeight) < 0.001f) _newHeight = _targetHeight;

            // In the air the feet tuck up instead of the head going down (HL2 crouch jump)
            if (!IsGrounded && !Mathf.Approximately(_newHeight, _oldHeight)) Handler.Rigidbody.position -= Vector3.up * (_newHeight - _oldHeight);

            currentHeight = _newHeight;
            ApplyColliderLayout();
        }

        private void ApplyColliderLayout() {
            // The capsule floats stepHeight above the feet; the ground check holds the feet, the capsule only meets walls
            float _height = Mathf.Max(Handler.Collider.radius * 2f, currentHeight - ColliderBottom);
            Vector3 _center = new(0f, ColliderBottom + _height * 0.5f, 0f);

            if (Mathf.Approximately(Handler.Collider.height, _height) && Handler.Collider.center == _center) return;

            Handler.Collider.height = _height;
            Handler.Collider.center = _center;
        }

        private void UpdateSlide() {
            float _speed = new Vector2(velocity.x, velocity.z).magnitude;

            if (!IsSliding) {
                IsSliding = IsGrounded && Handler.CrouchHeld && _speed >= Settings.slideStartSpeed;
                return;
            }

            if (!IsGrounded || !Handler.CrouchHeld || _speed < Settings.slideStopSpeed) IsSliding = false;
        }
        #endregion

        #region Actions
        private bool TryJump() {
            bool _canGroundJump = IsGrounded || coyoteTimer > 0f;
            bool _wantsGroundJump = JumpRequested || (Settings.autoBunnyhop && Handler.JumpHeld);

            if (_canGroundJump && _wantsGroundJump) {
                Jump();
                return true;
            }

            if (!_canGroundJump && JumpRequested && AirJumpsLeft > 0) {
                AirJumpsLeft--;
                Jump();
                return true;
            }

            return false;
        }

        private void Jump() {
            // Leaving the ground this tick skips friction, which is what keeps bunnyhop speed
            velocity.y = JumpVelocity;
            consumedJumpPressTime = Handler.LastJumpPressTime;
            coyoteTimer = 0f;

            groundSnapVelocity = 0f;

            IsGrounded = false;
            IsSliding = false;
        }

        private bool TryDash() {
            if (IsGrounded || DashesLeft <= 0 || !DashRequested) return false;

            Vector3 _direction = WishDirection(out float _inputAmount);
            if (_inputAmount <= 0.01f) _direction = Flatten(Handler.Orientation.forward);

            float _speed = Mathf.Max(Settings.dashSpeed, new Vector2(velocity.x, velocity.z).magnitude);

            // A clean horizontal burst: the fall is cut so the dash line is flat
            velocity = new Vector3(_direction.x * _speed, 0f, _direction.z * _speed);

            DashesLeft--;
            consumedDashPressTime = Handler.LastDashPressTime;
            return true;
        }
        #endregion

        #region Movement
        private void GroundMove(float _deltaTime) {
            if (IsSliding) {
                velocity = SourceMovement.ApplyFriction(velocity, Settings.slideFriction, Settings.stopSpeed, _deltaTime);

            } else {
                velocity = SourceMovement.ApplyFriction(velocity, Settings.friction, Settings.stopSpeed, _deltaTime);

                Vector3 _direction = WishDirection(out float _inputAmount);
                float _maxSpeed = IsCrouching ? Settings.crouchSpeed : Settings.maxSpeed;
                velocity = SourceMovement.Accelerate(velocity, _direction, _maxSpeed * _inputAmount, Settings.groundAcceleration, _deltaTime);
            }

            // Fixed ride height: horizontal speed is kept on slopes and steps, only the height is corrected.
            // The snap is remembered so the next ground check does not read it as a jump
            velocity.y = (groundHeight - Handler.Rigidbody.position.y) / _deltaTime;
            groundSnapVelocity = velocity.y;
        }

        private void AirMove(float _deltaTime, bool _applyGravity) {
            Vector3 _direction = WishDirection(out float _inputAmount);
            velocity = SourceMovement.AirAccelerate(
                velocity,
                _direction,
                Settings.maxSpeed * _inputAmount,
                Settings.airWishSpeedCap,
                Settings.airAcceleration,
                _deltaTime
            );

            if (_applyGravity) velocity.y -= Settings.gravity * _deltaTime;
        }

        private Vector3 WishDirection(out float _inputAmount) {
            Vector2 _input = Handler.MovementInput;
            Vector3 _wish = Flatten(Handler.Orientation.right) * _input.x + Flatten(Handler.Orientation.forward) * _input.y;

            _inputAmount = Mathf.Clamp01(_wish.magnitude);
            return _inputAmount > 0f ? _wish.normalized : Vector3.zero;
        }

        private static Vector3 Flatten(Vector3 _vector) {
            _vector.y = 0f;
            return _vector.normalized;
        }
        #endregion

        #region Ground Check Shape
        private Vector3 GroundCheckOrigin() {
            return Handler.Rigidbody.position + Handler.Collider.center;
        }

        private float GroundCheckRadius() {
            return Handler.Collider.radius * 0.95f;
        }

        private float GroundCheckLength(bool _wasGrounded) {
            // Reaches the feet plus a margin; a player already on the ground also reaches down one step
            float _toFeet = Handler.Collider.center.y - GroundCheckRadius();
            float _extra = _wasGrounded ? Settings.stepHeight : Settings.groundCheckDistance;
            return _toFeet + _extra;
        }
        #endregion
    }
}
