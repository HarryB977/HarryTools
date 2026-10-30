using System;
using UnityEngine;

namespace HarryTools.Platformer3DController {
    /// <summary>
    /// Physics based floating capsule controller (Toyful Games' approach, ported from Joe Binns' stylised character controller).
    /// A spring keeps the rigidbody at a ride height over the ground, another spring keeps it upright facing the input,
    /// and the horizontal movement is a clamped acceleration towards a goal velocity. Call Ticks from FixedUpdate.
    /// </summary>
    public class MovementController {
        [Serializable]
        public class Data {
            [Header("Ground Check")]
            [Tooltip("Radius of the sphere cast towards the ground")]
            public float groundCheckRadius = 0.25f;
            [Tooltip("Max distance from the rigidbody to the ground to count as grounded. Must be greater than the ride height")]
            public float groundCheckDistance = 1.2f;
            public LayerMask groundCheckMask = ~0;
            public float maxGroundAngle = 50f;

            [Header("Levitation")]
            [Tooltip("Distance from the rigidbody to the ground the spring settles at")]
            public float rideHeight = 1f;
            public float rideStiffness = 100f;
            public float rideDamper = 10f;

            [Header("Movement")]
            public float speed = 8f;
            public float sprintSpeed = 12f;
            public float acceleration = 100f;
            public float maxAcceleration = 50f;
            [Tooltip("Acceleration factor over the dot product between the input direction and the current velocity")]
            public AnimationCurve accelerationCurve = new(new Keyframe(-1f, 2f), new Keyframe(0f, 1f), new Keyframe(1f, 1f));
            [Tooltip("Height above the center where the movement force is applied, it tilts the body towards the acceleration")]
            public float lean = 0.5f;
            [Range(0f, 1f)] public float airControl = 1f;
            [Tooltip("Without input in the air the horizontal momentum is kept instead of braking")]
            public bool keepAirMomentum = true;

            [Header("Upright")]
            public float uprightStiffness = 500f;
            public float uprightDamper = 50f;

            [Header("Gravity")]
            public float gravityMultiplier = 4f;
            [Tooltip("Extra gravity multiplier while falling")]
            public float fallGravityMultiplier = 1f;

            [Header("Jump")]
            public float jumpHeight = 3f;
            public float jumpBufferTime = 0.1f;
            public float coyoteTime = 0.1f;
            [Tooltip("Time after a jump that never left the ground before jumping is allowed again")]
            public float failedJumpCooldown = 0.5f;

            [Header("Platforms")]
            [Tooltip("Keep the velocity of a MovingPlatform when leaving it")]
            public bool inheritPlatformVelocity = true;
            [Tooltip("Push the non kinematic rigidbodies the player stands on or jumps from")]
            public bool pushGroundRigidbodies = true;
        }

        // ==================== VARIABLES ===================
        #region Public Variables
        public bool IsGrounded { get; private set; } = false;
        public bool IsJumping { get; private set; } = false;
        public bool IsSprinting { get; private set; } = false;

        /// <summary> True while the ride height spring holds the player (grounded and not jumping). </summary>
        public bool IsLevitating => IsGrounded && !IsJumping;

        public GroundCheck Ground { get; } = new();
        public MovingPlatform Platform { get; private set; } = null;

        public Vector3 Up { get; private set; } = Vector3.up;
        public Vector3 FacingDirection { get; private set; } = Vector3.forward;
        public Vector3 Velocity => Handler.Rigidbody.linearVelocity;
        public float HorizontalSpeed => Vector3.ProjectOnPlane(Velocity, Up).magnitude;
        public float MaxSpeed => IsSprinting ? Settings.sprintSpeed : Settings.speed;

        public event Action OnJump;
        public event Action OnGrounded;
        public event Action OnUngrounded;
        #endregion

        #region Private Variables
        // Setup
        private IMovementHandler Handler { get; set; }

        // Movement
        private Vector3 goalVelocity = Vector3.zero;
        private Vector3 gravity = Vector3.zero;

        // Jump
        private float consumedJumpPressTime = float.NegativeInfinity;
        private float lastGroundedTime = float.NegativeInfinity;
        private float jumpTime = float.NegativeInfinity;

        // Platforms
        private Matrix4x4 platformMatrix = Matrix4x4.identity;
        private Vector3 platformVelocity = Vector3.zero;
        #endregion

        #region Properties
        private Data Settings => Handler.Data;
        private Rigidbody Body => Handler.Rigidbody;
        private float JumpVelocity => Mathf.Sqrt(2f * gravity.magnitude * Settings.jumpHeight);

        private bool JumpRequested => Handler.LastJumpPressTime > consumedJumpPressTime && Time.time - Handler.LastJumpPressTime <= Settings.jumpBufferTime;
        private bool CanJump => !IsJumping && (IsGrounded || Time.time - lastGroundedTime <= Settings.coyoteTime);
        #endregion

        // ==================== START ====================
        public MovementController(IMovementHandler _handler) {
            // Setup
            Handler = _handler;

            // The controller applies its own gravity, so the levitation can cancel it
            Body.useGravity = false;

            Vector3 _forward = Vector3.ProjectOnPlane(Body.transform.forward, Vector3.up);
            if (_forward.sqrMagnitude > 1e-4f) FacingDirection = _forward.normalized;
        }

        public void Ticks(float _deltaTime) {
            if (Handler == null || _deltaTime <= 0f) return;

            // Gravity
            Vector3 _worldGravity = Physics.gravity;
            Up = _worldGravity.sqrMagnitude > 1e-6f ? -_worldGravity.normalized : Vector3.up;
            gravity = _worldGravity * Settings.gravityMultiplier;

            // Checks
            CheckGround();
            FollowPlatform(_deltaTime);

            // Actions
            TryJump();

            // Forces
            ApplyVerticalForces();
            Move(_deltaTime);
            KeepUpright();
        }

        public void ShowGizmos() {
            if (Handler == null) return;

            // Ground Check
            Ground.ShowGizmos();

            // Ride Height
            if (IsGrounded) {
                Gizmos.color = Color.white;
                Gizmos.DrawWireSphere(Ground.Origin + Ground.Direction * (Ground.Distance - Settings.rideHeight), 0.1f);
            }

            // Facing
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(Body.position, Body.position + FacingDirection);
        }

        // ==================== METHODS ====================
        #region Checks
        private void CheckGround() {
            bool _wasGrounded = IsGrounded;

            IsGrounded = Ground.Check(
                Body.position,
                -Up,
                Settings.groundCheckRadius,
                Settings.groundCheckDistance,
                Settings.groundCheckMask,
                Body,
                Settings.maxGroundAngle
            );

            if (IsGrounded) lastGroundedTime = Time.time;

            // A jump ends when the player lands again, or after a cooldown if it never left the ground
            if (IsJumping && IsGrounded && (!_wasGrounded || Time.time - jumpTime > Settings.failedJumpCooldown)) IsJumping = false;

            if (IsGrounded && !_wasGrounded) OnGrounded?.Invoke();
            else if (!IsGrounded && _wasGrounded) OnUngrounded?.Invoke();
        }
        #endregion

        #region Platforms
        private void FollowPlatform(float _deltaTime) {
            MovingPlatform _platform = IsGrounded ? Ground.Hit.collider.GetComponentInParent<MovingPlatform>() : null;

            if (_platform != Platform) {
                // Leaving a platform keeps its motion instead of stopping dead in the air
                if (Platform != null && _platform == null && Settings.inheritPlatformVelocity) Body.linearVelocity += platformVelocity;

                Platform = _platform;
                platformVelocity = Vector3.zero;
                if (Platform != null) platformMatrix = Platform.transform.localToWorldMatrix;
                return;
            }

            if (Platform == null) return;

            // Moving with the platform as a transform delta works for any mover and never drifts
            Matrix4x4 _matrix = Platform.transform.localToWorldMatrix;
            Matrix4x4 _delta = _matrix * platformMatrix.inverse;
            platformMatrix = _matrix;

            Vector3 _position = _delta.MultiplyPoint3x4(Body.position);
            platformVelocity = (_position - Body.position) / _deltaTime;
            Body.position = _position;

            // Only the yaw is carried, the upright spring owns the tilt
            Quaternion _yaw = YawOf(_delta.rotation);
            Body.rotation = _yaw * Body.rotation;
            FacingDirection = _yaw * FacingDirection;
            goalVelocity = _yaw * goalVelocity;
        }

        private Quaternion YawOf(Quaternion _rotation) {
            Vector3 _reference = Vector3.ProjectOnPlane(Vector3.forward, Up);
            if (_reference.sqrMagnitude < 1e-4f) _reference = Vector3.ProjectOnPlane(Vector3.right, Up);

            Vector3 _rotated = Vector3.ProjectOnPlane(_rotation * _reference, Up);
            if (_rotated.sqrMagnitude < 1e-4f) return Quaternion.identity;

            return Quaternion.AngleAxis(Vector3.SignedAngle(_reference, _rotated, Up), Up);
        }

        private Rigidbody PushableGround() {
            if (!IsGrounded || !Settings.pushGroundRigidbodies) return null;

            Rigidbody _rigidbody = Ground.Hit.rigidbody;
            return _rigidbody != null && !_rigidbody.isKinematic ? _rigidbody : null;
        }
        #endregion

        #region Jump
        private void TryJump() {
            if (!JumpRequested || !CanJump) return;

            // Only the vertical velocity is replaced, the horizontal momentum is kept
            float _upSpeed = Vector3.Dot(Body.linearVelocity, Up);
            Vector3 _deltaVelocity = Up * (JumpVelocity - _upSpeed);
            Body.AddForce(_deltaVelocity, ForceMode.VelocityChange);

            Rigidbody _ground = PushableGround();
            if (_ground != null) _ground.AddForceAtPosition(-_deltaVelocity * Body.mass, Ground.Hit.point, ForceMode.Impulse);

            consumedJumpPressTime = Handler.LastJumpPressTime;
            lastGroundedTime = float.NegativeInfinity;
            jumpTime = Time.time;
            IsJumping = true;

            OnJump?.Invoke();
        }
        #endregion

        #region Forces
        private void ApplyVerticalForces() {
            if (!IsLevitating) {
                bool _falling = Vector3.Dot(Body.linearVelocity, Up) < 0f;
                Body.AddForce(_falling ? gravity * Settings.fallGravityMultiplier : gravity, ForceMode.Acceleration);
                return;
            }

            // Spring towards the ride height, damped by the speed relative to the ground
            Vector3 _down = -Up;
            Vector3 _groundVelocity = Platform == null && Ground.Hit.rigidbody != null ? Ground.Hit.rigidbody.GetPointVelocity(Ground.Hit.point) : Vector3.zero;
            float _relativeSpeed = Vector3.Dot(Body.linearVelocity - _groundVelocity, _down);
            float _error = Ground.Distance - Settings.rideHeight;
            float _spring = _error * Settings.rideStiffness - _relativeSpeed * Settings.rideDamper;

            // Gravity is not applied while levitating, the spring alone holds the ride height
            Vector3 _acceleration = _down * _spring;
            Body.AddForce(_acceleration, ForceMode.Acceleration);

            // The ground takes the opposite of the support force, which also carries the player's weight
            Rigidbody _ground = PushableGround();
            if (_ground != null) _ground.AddForceAtPosition((gravity - _acceleration) * Body.mass, Ground.Hit.point, ForceMode.Force);
        }

        private void Move(float _deltaTime) {
            Vector3 _input = WorldInput();
            IsSprinting = Handler.SprintHeld;

            if (_input.sqrMagnitude > 1e-4f) FacingDirection = _input.normalized;

            Vector3 _horizontalVelocity = Vector3.ProjectOnPlane(Body.linearVelocity, Up);
            if (!IsGrounded && Settings.keepAirMomentum && _input.sqrMagnitude <= 1e-4f) {
                goalVelocity = _horizontalVelocity;
                return;
            }

            // Turning against the current velocity accelerates harder than speeding up
            float _alignment = Vector3.Dot(_input.normalized, _horizontalVelocity.normalized);
            float _factor = Settings.accelerationCurve.Evaluate(_alignment) * (IsGrounded ? 1f : Settings.airControl);

            goalVelocity = Vector3.MoveTowards(goalVelocity, _input * MaxSpeed, _factor * Settings.acceleration * _deltaTime);
            goalVelocity = Vector3.ProjectOnPlane(goalVelocity, Up);

            Vector3 _acceleration = (goalVelocity - _horizontalVelocity) / _deltaTime;
            _acceleration = Vector3.ClampMagnitude(_acceleration, _factor * Settings.maxAcceleration);

            // Pushing above the center leans the body into the movement
            Body.AddForceAtPosition(_acceleration * Body.mass, Body.worldCenterOfMass + Up * Settings.lean, ForceMode.Force);
        }

        private void KeepUpright() {
            Quaternion _goal = Quaternion.LookRotation(FacingDirection, Up);
            Quaternion _error = ShortestRotation(_goal, Body.rotation);
            _error.ToAngleAxis(out float _degrees, out Vector3 _axis);

            Vector3 _torque = -Body.angularVelocity * Settings.uprightDamper;
            if (IsFinite(_axis)) _torque += _axis.normalized * (_degrees * Mathf.Deg2Rad * Settings.uprightStiffness);

            Body.AddTorque(_torque, ForceMode.Acceleration);
        }

        private Vector3 WorldInput() {
            Vector3 _forward = Vector3.ProjectOnPlane(Handler.Orientation.forward, Up);

            // Looking straight down, the camera's up gives the forward direction
            if (_forward.sqrMagnitude < 1e-4f) _forward = Vector3.ProjectOnPlane(Handler.Orientation.up, Up);

            _forward.Normalize();
            Vector3 _right = Vector3.Cross(Up, _forward);

            Vector2 _input = Vector2.ClampMagnitude(Handler.MovementInput, 1f);
            return _right * _input.x + _forward * _input.y;
        }
        #endregion

        #region Utils
        /// <summary> Rotation from _b to _a through the shortest path. </summary>
        private static Quaternion ShortestRotation(Quaternion _a, Quaternion _b) {
            if (Quaternion.Dot(_a, _b) < 0f) _b = new Quaternion(-_b.x, -_b.y, -_b.z, -_b.w);
            return _a * Quaternion.Inverse(_b);
        }

        private static bool IsFinite(Vector3 _vector) {
            return float.IsFinite(_vector.x) && float.IsFinite(_vector.y) && float.IsFinite(_vector.z) && _vector.sqrMagnitude > 1e-6f;
        }
        #endregion
    }
}
