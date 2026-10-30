using System;
using System.Collections.Generic;
using UnityEngine;

namespace HarryTools.Platformer2DController {
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D), typeof(CapsuleCollider2D))]
    [RequireComponent(typeof(ConstantForce2D))]
    public abstract class MovementController : MonoBehaviour {
        public struct MovementState {
            public bool isGrounded;

            public Vector2 velocity;
            public Vector2 position;
            public float rotation;
        }

        public enum JumpType {
            Jump, Coyote, AirJump, WallJump
        }

        private enum ColliderMode {
            Standard, Crouching, Airborne
        }

        // ==================== VARIABLES ===================
        #region Properties
        // Collisions
        private float GrounderLength => controllerSize.StepHeight + SKIN_WIDTH;
        private Vector2 RayPoint => framePosition + UpVector * (controllerSize.StepHeight + SKIN_WIDTH);

        // Walls
        private bool HorizontalInputPresset => Mathf.Abs(MoveInput.x) > Data.HorizontalDeadZoneThreshold;
        private bool IsPushingAgainstWall => HorizontalInputPresset && (int)Mathf.Sign(frameDirection.x) == wallDirThisFrame;

        // Ladders
        private bool CanEnterLadder => ladderHit && loopTime > timeLeftLadder + Data.LadderCooldownTime;
        private bool ShouldMountLadder => Data.AutoAttachToLadders || MoveInput.y > Data.VerticalDeadZoneThreshold || (grounded && MoveInput.y < -Data.VerticalDeadZoneThreshold);
        private bool ShouldDismountLadder => !Data.AutoAttachToLadders && grounded && MoveInput.y < -Data.VerticalDeadZoneThreshold;

        // Jump
        private bool IsWithinJumpClearance => lastJumpExecutedTime + JUMP_CLEARANCE_TIME > loopTime;
        private bool HasBufferedJump => bufferedJumpUsable && loopTime < timeJumpWasPressed + Data.BufferedJumpTime && !IsWithinJumpClearance;
        private bool CanUseCoyote => coyoteUsable && !grounded && loopTime < timeLeftGrounded + Data.CoyoteTime;
        private bool CanAirJump => !grounded && airJumpsRemaining > 0;
        private bool CanWallJump => grounded && (isOnWall || wallDirThisFrame != 0) || (wallJumpCoyoteUsable && loopTime < timeLeftWall + Data.WallCoyoteTime);

        private bool CrouchPressed => MoveInput.y < -Data.VerticalDeadZoneThreshold;

        // Crouching
        private bool CanStand => IsStandingPosClear(rb.position + controllerSize.StandingColliderCenter);
        #endregion

        #region Public Variables
        // Events
        public event Action<bool> OnToggledPlayer;
        public event Action<JumpType> OnJump;
        public event Action<bool, float> OnGroundedChanged;
        public event Action<bool, Vector2> OnDashChange;
        public event Action<bool> OnWallGrabChanged;
        public event Action<Vector2> OnRepositioned;

        // States
        public MovementState CurrentState { get; private set; }

        public bool IsActive { get; private set; } = true;
        public bool IsCrouching { get; private set; } = false;
        public bool IsClimbingLadder { get; private set; } = false;

        public Vector2 UpVector { get; private set; }
        public Vector2 RightVector { get; private set; }
        public Vector2 Velocity { get; private set; }

        public Vector2 GroundNormal { get; private set; }
        public int WallDirection { get; private set; }
        #endregion

        #region Private Variables
        // Settings
        protected abstract ControllerData Data { get; }

        // References
        private BoxCollider2D boxCollider;
        private CapsuleCollider2D capsuleCollider;
        private ConstantForce2D forceConstant;
        private Rigidbody2D rb;

        // Loop
        private float loopDelta;
        private float loopTime;

        // Setup
        private const float GRAVITY_SCALE = 1f;

        private bool cachedQueryMode;
        private bool cachedQueryTriggers;

        private ControllerData.GeneratedSize controllerSize;

        // Inputs
        protected abstract Vector2 MoveInput { get; }
        protected abstract bool JumpDownInput { get; }
        protected abstract bool JumpHeldInput { get; }
        protected abstract bool DashDownInput { get; }

        // Frame Data
        private bool hasInputThisFrame;
        private Vector2 trimmedFrameVelocity;
        private Vector2 framePosition;
        private Bounds wallDetectionBounds;

        // Collisions
        private const float SKIN_WIDTH = 0.02f;
        private const int RAY_SIDE_COUNT = 5;

        private RaycastHit2D groundHit;
        private bool grounded;
        private float currentStepDownLength;

        // Direction
        private Vector2 frameDirection;

        // Walls
        private const float WALL_REATTACH_COOLDOWN = 0.2f;

        private bool isOnWall;
        private float wallJumpInputNerfPoint;
        private float timeLeftWall;
        private float canGrabWallAfter;
        private int wallDirectionForJump;
        private int wallDirThisFrame;

        // Ladders
        private float timeLeftLadder;
        private float ladderSnapVel;
        private Collider2D ladderHit;

        // Jump
        private const float JUMP_CLEARANCE_TIME = 0.25f;

        private bool jumpToConsume;
        private bool bufferedJumpUsable;
        private bool endedJumpEarly;
        private bool wallJumpCoyoteUsable;
        private bool coyoteUsable;
        private float lastJumpExecutedTime;
        private float timeJumpWasPressed;
        private float timeLeftGrounded;
        private float returnWallInputLossAfter;
        private Vector2 forceToApplyThisFrame;
        private int airJumpsRemaining;

        // Dash
        private bool dashToConsume;
        private bool canDash;
        private bool dashing;
        private Vector2 dashVelocity;
        private float startedDashing;
        private float nextDashTime;

        // Crouching
        private float timeStartedCrouching;

        // Move
        private const float SLOPE_ANGLE_FOR_EXACT_MOVEMENT = 0.7f;

        private Vector2 frameTransientVelocity;
        private Vector2 immediateMove;
        private Vector2 decayingTransientVelocity;
        private Vector2 totalTransientVelocityAppliedLastFrame;
        private Vector2 frameSpeedModifier = Vector2.zero;
        private Vector2 currentFrameSpeedModifier = Vector2.zero;
        private float lastFrameY;

        private IPhysicsObjectMove lastPlatform;

        // External Triggers
        private const int MAX_ACTIVE_MOVERS = 5;
        private readonly HashSet<IPhysicsObjectMove> currentPlatforms = new(MAX_ACTIVE_MOVERS);
        private readonly HashSet<ISpeedModifier> modifiers = new();

        private Vector2 frameSpeedModifierVelocity;
        #endregion

        // ==================== START ====================
        protected void MovementTicks() {
            loopDelta = Time.deltaTime;
            loopTime += loopDelta;

            GatherInput();
        }

        protected void MovementFixedTicks() {
            loopDelta = Time.deltaTime;

            if (!IsActive) return;

            RemoveTransientVelocity();

            SetFrameData();

            CalculateCollisions();
            CalculateDirection();

            CalculateWalls();
            CalculateLadders();
            CalculateJump();
            CalculateDash();

            CalculateExternalModifiers();

            TraceGround();
            Move();

            CalculateCrouch();

            CleanFrameData();

            SaveCharacterState();
        }

        protected virtual void OnTriggerEnter2D(Collider2D _other) {
            if (_other.TryGetComponent(out ISpeedModifier _modifier)) modifiers.Add(_modifier);
            else if (_other.TryGetComponent(out IPhysicsObjectMove _mover) && !_mover.RequireGrounding) currentPlatforms.Add(_mover);
        }

        protected virtual void OnTriggerExit2D(Collider2D _other) {
            if (_other.TryGetComponent(out ISpeedModifier _modifier)) modifiers.Remove(_modifier);
            else if (_other.TryGetComponent(out IPhysicsObjectMove _mover)) currentPlatforms.Remove(_mover);
        }

        protected virtual void OnDrawGizmos() {
            Vector2 _position = (Vector2)transform.position;

            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(_position + Vector2.up * controllerSize.Height / 2, new Vector3(controllerSize.Width, controllerSize.Height));
            Gizmos.color = Color.magenta;

            Vector2 _rayStart = _position + Vector2.up * controllerSize.StepHeight;
            Vector3 _rayDir = Vector3.down * controllerSize.StepHeight;

            Gizmos.DrawRay(_rayStart, _rayDir);

            foreach (float _offset in GenerateRayOffsets()) {
                Gizmos.DrawRay(_rayStart + Vector2.right * _offset, _rayDir);
                Gizmos.DrawRay(_rayStart + Vector2.left * _offset, _rayDir);
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(_position + (Vector2)wallDetectionBounds.center, wallDetectionBounds.size);

            Gizmos.color = Color.black;
            Gizmos.DrawRay(RayPoint, Vector3.right);
        }

        // ==================== METHODS ====================
        #region Setup
        protected void InitializeController() {
            // References
            TryGetComponent(out boxCollider);
            TryGetComponent(out capsuleCollider);
            TryGetComponent(out forceConstant);
            TryGetComponent(out rb);

            // Loop
            loopDelta = 0;
            loopTime = 0;

            // Update
            UpdateController();
        }

        protected void UpdateController() {
            controllerSize = Data.ControllerSize.GenerateCharacterSize();
            cachedQueryMode = Physics2D.queriesStartInColliders;

            wallDetectionBounds = new Bounds(
                new Vector3(0, controllerSize.Height / 2),
                new Vector3(controllerSize.StandingColliderCenter.x + ControllerData.Size.COLLIDER_EDGE_RADIUS * 2 + Data.WallDetectorRange, controllerSize.Height - 0.1f)
            );

            // Primary Collider
            boxCollider.edgeRadius = ControllerData.Size.COLLIDER_EDGE_RADIUS;
            boxCollider.hideFlags = HideFlags.NotEditable;
            boxCollider.sharedMaterial = rb.sharedMaterial;
            boxCollider.enabled = true;

            // Airborne collider
            capsuleCollider.hideFlags = HideFlags.NotEditable;
            capsuleCollider.size = new Vector2(controllerSize.Width - SKIN_WIDTH * 2, controllerSize.Height - SKIN_WIDTH * 2);
            capsuleCollider.offset = new Vector2(0, controllerSize.Height / 2);
            capsuleCollider.sharedMaterial = rb.sharedMaterial;

            SetColliderMode(ColliderMode.Airborne);
        }
        #endregion

        // =================================================

        #region Input
        private void GatherInput() {
            if (JumpDownInput) {
                jumpToConsume = true;
                timeJumpWasPressed = loopTime;
            }

            if (DashDownInput) {
                dashToConsume = true;
            }
        }
        #endregion

        // =================================================

        #region State
        public void LoadState(MovementState _state) {
            RepositionImmediately(_state.position);
            rb.rotation = _state.rotation;

            SetVelocity(_state.velocity);

            if (_state.isGrounded) {
                ToggleGrounded(true);
            }
        }

        public void RepositionImmediately(Vector2 _position, bool _resetVelocity = false) {
            rb.position = _position;

            if (_resetVelocity) {
                SetVelocity(Vector2.zero);
            }

            OnRepositioned?.Invoke(_position);
        }

        public void TogglePlayer(bool _state) {
            IsActive = _state;

            rb.bodyType = !_state ? RigidbodyType2D.Kinematic : RigidbodyType2D.Dynamic;
            OnToggledPlayer?.Invoke(_state);
        }
        #endregion

        // =================================================

        #region Frame Data
        public void AddFrameForce(Vector2 _force, bool _resetVelocity = false) {
            if (_resetVelocity) SetVelocity(Vector2.zero);
            forceToApplyThisFrame += _force;
        }

        private void SetFrameData() {
            float _rotation = rb.rotation * Mathf.Deg2Rad;
            UpVector = new Vector2(-Mathf.Sign(_rotation), Mathf.Cos(_rotation));
            RightVector = new Vector2(UpVector.y, -UpVector.x);

            framePosition = rb.position;
            hasInputThisFrame = MoveInput.x != 0;

            Velocity = rb.linearVelocity;
            trimmedFrameVelocity = new Vector2(Velocity.x, 0);
        }

        private void RemoveTransientVelocity() {
            Vector2 _currentVelocity = rb.linearVelocity;
            Vector2 _velocityBeforeReduction = _currentVelocity;

            _currentVelocity -= totalTransientVelocityAppliedLastFrame;
            SetVelocity(_currentVelocity);

            frameTransientVelocity = Vector2.zero;
            totalTransientVelocityAppliedLastFrame = Vector2.zero;

            float _decay = Data.Friction * Data.AirFrictionMultiplier * Data.ExternalVelocityDecayRate;
            if (
                (_velocityBeforeReduction.x < 0 && decayingTransientVelocity.x < _velocityBeforeReduction.x) ||
                (_velocityBeforeReduction.x > 0 && decayingTransientVelocity.x > _velocityBeforeReduction.x) ||
                (_velocityBeforeReduction.x < 0 && decayingTransientVelocity.y < _velocityBeforeReduction.y) ||
                (_velocityBeforeReduction.x > 0 && decayingTransientVelocity.y > _velocityBeforeReduction.y)

            ) _decay *= 5;

            decayingTransientVelocity = Vector2.MoveTowards(decayingTransientVelocity, Vector2.zero, _decay * loopDelta);
            immediateMove = Vector2.zero;
        }

        private void CleanFrameData() {
            jumpToConsume = false;
            dashToConsume = false;
            forceToApplyThisFrame = Vector2.zero;
            lastFrameY = Velocity.y;
        }
        #endregion

        // =================================================

        #region Collisions
        private void CalculateCollisions() {
            Physics2D.queriesStartInColliders = false;
            bool _isGroundedThisFrame = PerformRay(RayPoint);

            if (!_isGroundedThisFrame) {
                foreach (float _offset in GenerateRayOffsets()) {
                    _isGroundedThisFrame = PerformRay(RayPoint + RightVector * _offset) || PerformRay(RayPoint - RightVector * _offset);
                    if (_isGroundedThisFrame) break;
                }
            }

            if (_isGroundedThisFrame && !grounded) ToggleGrounded(true);
            else if (!_isGroundedThisFrame && grounded) ToggleGrounded(false);

            Physics2D.queriesStartInColliders = cachedQueryMode;

            bool PerformRay(Vector2 _point) {
                groundHit = Physics2D.Raycast(_point, -UpVector, GrounderLength + currentStepDownLength, Data.CollisionLayerMask);
                if (!groundHit) return false;

                if (Vector2.Angle(groundHit.normal, UpVector) > Data.MaxWalkableSlope) {
                    return false;
                }

                return true;
            }
        }

        private IEnumerable<float> GenerateRayOffsets() {
            float _extent = controllerSize.StandingColliderSize.x / 2 - controllerSize.RayInset;
            float _offsetAmount = _extent / RAY_SIDE_COUNT;

            for (int i = 0; i < RAY_SIDE_COUNT; i++) {
                yield return _offsetAmount * i;
            }
        }

        private void ToggleGrounded(bool _state) {
            grounded = _state;

            if (_state) {
                OnGroundedChanged?.Invoke(true, lastFrameY);
                rb.gravityScale = 0;

                SetVelocity(trimmedFrameVelocity);

                forceConstant.force = Vector2.zero;
                currentStepDownLength = controllerSize.StepHeight;
                canDash = true;
                coyoteUsable = true;
                bufferedJumpUsable = true;

                ResetAirJumps();
                SetColliderMode(ColliderMode.Standard);

            } else {
                OnGroundedChanged?.Invoke(false, 0);
                timeLeftGrounded = loopTime;
                rb.gravityScale = GRAVITY_SCALE;

                SetColliderMode(ColliderMode.Airborne);
            }
        }

        private void SetColliderMode(ColliderMode _mode) {
            capsuleCollider.enabled = _mode == ColliderMode.Airborne;

            switch (_mode) {
                case ColliderMode.Standard:
                    boxCollider.size = controllerSize.StandingColliderSize;
                    boxCollider.offset = controllerSize.StandingColliderCenter;

                    break;

                case ColliderMode.Crouching:
                    boxCollider.size = controllerSize.CrouchColliderSize;
                    boxCollider.offset = controllerSize.CrouchingColliderCenter;

                    break;
            }
        }
        #endregion

        // =================================================

        #region Direction
        private void CalculateDirection() {
            frameDirection = new Vector2(MoveInput.x, 0);
            if (grounded) {
                GroundNormal = groundHit.normal;
                float _angle = Vector2.Angle(GroundNormal, UpVector);

                if (_angle < Data.MaxWalkableSlope) {
                    frameDirection.y = frameDirection.x * -GroundNormal.x / GroundNormal.y;
                }

                frameDirection = frameDirection.normalized;
            }
        }
        #endregion

        // =================================================

        #region Walls
        private void CalculateWalls() {
            if (!Data.AllowWallJump) return;

            float _rayDirection = isOnWall ? WallDirection : frameDirection.x;
            bool _hasHitWall = DetectWallCast(_rayDirection);

            wallDirThisFrame = _hasHitWall ? (int)_rayDirection : 0;

            if (!isOnWall && ShouldStickToWall() && loopTime > canGrabWallAfter && Velocity.y < 0) ToggleOnWall(true);
            else if (isOnWall && !ShouldStickToWall()) ToggleOnWall(false);

            bool ShouldStickToWall() {
                if (wallDirThisFrame == 0 || grounded) return false;
                if (HorizontalInputPresset && !IsPushingAgainstWall) return false;

                return !Data.RequireInputPush || IsPushingAgainstWall;
            }
        }

        private bool DetectWallCast(float _direction) {
            return Physics2D.BoxCast(
                framePosition + (Vector2)wallDetectionBounds.center,
                new Vector2(controllerSize.StandingColliderSize.x - SKIN_WIDTH, wallDetectionBounds.size.y),
                0,
                new Vector2(_direction, 0), Data.WallDetectorRange,
                Data.ClimbLayerMask
            );
        }

        private void ToggleOnWall(bool _state) {
            isOnWall = _state;

            if (_state) {
                decayingTransientVelocity = Vector2.zero;
                bufferedJumpUsable = true;
                wallJumpCoyoteUsable = true;

                WallDirection = wallDirThisFrame;

            } else {
                timeLeftWall = loopTime;
                canGrabWallAfter = loopTime + WALL_REATTACH_COOLDOWN;
                rb.gravityScale = GRAVITY_SCALE;

                WallDirection = 0;

                if (Velocity.y > 0) {
                    AddFrameForce(new Vector2(0, Data.WallPopForce), true);
                }

                ResetAirJumps(); // So that we can air jump even if we didn't leave via a wall jump
            }

            OnWallGrabChanged?.Invoke(_state);
        }
        #endregion

        // =================================================

        #region Ladders
        private void CalculateLadders() {
            if (!Data.AllowLadders) return;

            Physics2D.queriesHitTriggers = true;
            ladderHit = Physics2D.OverlapBox(
                framePosition + (Vector2)wallDetectionBounds.center,
                wallDetectionBounds.size,
                0,
                Data.LadderLayerMask
            );

            Physics2D.queriesHitTriggers = cachedQueryTriggers;
            if (!IsClimbingLadder && CanEnterLadder && ShouldMountLadder) ToggleClimbingLadder(true);
            else if (IsClimbingLadder && (!ladderHit || ShouldDismountLadder)) ToggleClimbingLadder(false);
        }

        private void ToggleClimbingLadder(bool _state) {
            if (IsClimbingLadder == _state) return;

            if (_state) {
                SetVelocity(Vector2.zero);

                rb.gravityScale = 0;
                ladderSnapVel = 0;

            } else {
                if (ladderHit) timeLeftLadder = loopTime;
                if (MoveInput.y > 0) {
                    AddFrameForce(new Vector2(0, Data.LadderPopForce));
                }

                rb.gravityScale = GRAVITY_SCALE;
            }

            IsClimbingLadder = _state;
            ResetAirJumps();
        }
        #endregion

        // =================================================

        #region Jump
        private void CalculateJump() {
            if ((jumpToConsume || HasBufferedJump) && CanStand) {
                if (CanWallJump) ExecuteJump(JumpType.WallJump);
                else if (grounded || IsClimbingLadder) ExecuteJump(JumpType.Jump);
                else if (CanUseCoyote) ExecuteJump(JumpType.Coyote);
                else if (CanAirJump) ExecuteJump(JumpType.AirJump);
            }

            if ((!endedJumpEarly && !grounded && !JumpHeldInput && Velocity.y > 0) || Velocity.y < 0) endedJumpEarly = true;


            if (loopTime > returnWallInputLossAfter) wallJumpInputNerfPoint = Mathf.MoveTowards(wallJumpInputNerfPoint, 1, loopDelta / Data.WallJumpInputLossReturnTime);
        }

        private void ExecuteJump(JumpType _type) {
            SetVelocity(trimmedFrameVelocity);

            endedJumpEarly = false;
            bufferedJumpUsable = false;
            lastJumpExecutedTime = loopTime;
            currentStepDownLength = 0;

            if (IsClimbingLadder) {
                ToggleClimbingLadder(false);
            }

            switch (_type) {
                case JumpType.Jump:
                    coyoteUsable = false;
                    AddFrameForce(new Vector2(0, Data.JumpPower));
                    break;
                
                case JumpType.Coyote:
                    coyoteUsable = false;
                    AddFrameForce(new Vector2(0, Data.JumpPower));
                    break;

                case JumpType.AirJump:
                    airJumpsRemaining--;
                    AddFrameForce(new Vector2(0, Data.JumpPower));

                    break;
                
                case JumpType.WallJump:
                    ToggleOnWall(false);

                    wallJumpCoyoteUsable = false;
                    wallJumpInputNerfPoint = 0;
                    returnWallInputLossAfter = loopTime + Data.WallJumpTotalInputLossTime;
                    wallDirectionForJump = wallDirThisFrame;

                    AddFrameForce(
                        new Vector2(-wallDirThisFrame, 1) * (isOnWall || IsPushingAgainstWall ? Data.WallJumpPower : Data.WallPushPower)
                    );

                    break;
            }

            OnJump?.Invoke(_type);
        }

        private void ResetAirJumps() {
            airJumpsRemaining = Data.MaxAirJumps;
        }
        #endregion

        // =================================================

        #region Dash
        private void CalculateDash() {
            if (!Data.AllowDash) return;
            if (dashToConsume && canDash && !IsCrouching && loopTime > nextDashTime) {
                Vector2 _direction = new Vector2(MoveInput.x, Mathf.Max(MoveInput.y, 0f)).normalized;
                if (_direction == Vector2.zero) return;

                dashVelocity = _direction * Data.DashVelocity;
                dashing = true;
                canDash = false;
                startedDashing = loopTime;
                nextDashTime = loopTime + Data.DashCooldown;

                OnDashChange?.Invoke(true, _direction);
            }

            if (dashing) {
                if (loopTime > startedDashing + Data.DashDuration) {
                    dashing = false;

                    OnDashChange?.Invoke(false, Vector2.zero);

                    SetVelocity(new Vector2(Velocity.x * Data.DashEndHorizontalMultiplier, Velocity.y));
                    if (grounded) canDash = true;
                }
            }
        }
        #endregion

        // =================================================

        #region Crouching
        private bool IsStandingPosClear(Vector2 _position) {
            return CheckPos(_position, controllerSize.StandingColliderSize - SKIN_WIDTH * Vector2.one);
        }

        private void CalculateCrouch() {
            if (!Data.AllowCrouching) return;

            if (!IsCrouching && CrouchPressed && grounded) ToggleCrouching(true);
            else if (IsCrouching && (!CrouchPressed || !grounded)) ToggleCrouching(false);
        }

        private void ToggleCrouching(bool _state) {
            if (_state) {
                timeStartedCrouching = loopTime;
                IsCrouching = true;

            } else {
                if (!CanStand) return;
                IsCrouching = false;
            }

            SetColliderMode(IsCrouching ? ColliderMode.Crouching : ColliderMode.Standard);
        }

        private bool CheckPos(Vector2 _position, Vector2 _size) {
            Physics2D.queriesHitTriggers = false;
            Collider2D _hit = Physics2D.OverlapBox(_position, _size, 0, Data.CollisionLayerMask);
            Physics2D.queriesHitTriggers = cachedQueryMode;

            return !_hit;
        }
        #endregion

        // =================================================

        #region Move
        private void TraceGround() {
            IPhysicsObjectMove _currentPlatform = null;

            if (grounded && !IsWithinJumpClearance) {
                float _distanceFromGround = controllerSize.StepHeight - groundHit.distance;

                if (_distanceFromGround != 0) {
                    Vector2 _requireMove = Vector2.zero;
                    _requireMove.y += _distanceFromGround;

                    if (Data.PositionCorrection == ControllerData.PositionCorrectionMode.Velocity) frameTransientVelocity = _requireMove / loopDelta;
                    else immediateMove = _requireMove;
                }

                if (groundHit.transform.TryGetComponent(out _currentPlatform)) {
                    currentPlatforms.Add(_currentPlatform);
                }
            }

            if (lastPlatform != _currentPlatform) {
                if (lastPlatform is { UsesBounding: false }) {
                    currentPlatforms.Remove(lastPlatform);
                    ApplyObjectMoveExitVelocity(lastPlatform);
                }

                lastPlatform = _currentPlatform;
            }

            foreach (IPhysicsObjectMove _platform in currentPlatforms) {
                if (framePosition.y < _platform.FramePosition.y - SKIN_WIDTH) continue;
                frameTransientVelocity += _platform.FramePositionDelta / loopDelta;
            }
        }

        private void ApplyObjectMoveExitVelocity(IPhysicsObjectMove _object) {
            Vector2 _platformVelocity = _object.TakeOffVelocity;
            if (_platformVelocity.y < 0) {
                _platformVelocity.y *= Data.NegativeYVelocityNegation;
            }

            decayingTransientVelocity += _platformVelocity;
        }

        private void Move() {
            if (forceToApplyThisFrame != Vector2.zero) {
                rb.linearVelocity += AdditionalFrameVelocities();
                rb.AddForce(forceToApplyThisFrame * rb.mass, ForceMode2D.Impulse);

                return;
            }

            if (dashing) {
                SetVelocity(dashVelocity);
                return;
            }

            if (isOnWall) {
                forceConstant.force = Vector2.zero;

                float _wallVelocity;

                if (MoveInput.y != 0) _wallVelocity = MoveInput.y * Data.WallClimbSpeed;
                else _wallVelocity = Mathf.MoveTowards(Mathf.Min(Velocity.y, 0), -Data.WallClimbSpeed, Data.WallFallAcceleration * loopDelta);

                SetVelocity(new Vector2(rb.linearVelocity.x, _wallVelocity));
                return;
            }

            if (IsClimbingLadder) {
                forceConstant.force = Vector2.zero;
                rb.gravityScale = 0;

                Vector2 _goalVelocity = Vector2.zero;
                _goalVelocity.y = MoveInput.y * (MoveInput.y > 0 ? Data.LadderClimbSpeed : Data.LadderSlideSpeed);

                // Horizontal
                float _goalX;

                if (!Data.SnapToLadders || MoveInput.x != 0) _goalX = Mathf.MoveTowards(framePosition.x, framePosition.x + MoveInput.x, Data.Acceleration * Data.LadderShimmySpeedMultiplier * loopDelta);
                else {
                    float _targetX = ladderHit.transform.position.x;
                    _goalX = Mathf.SmoothDamp(framePosition.x, _targetX, ref ladderSnapVel, Data.LadderSnapTime);
                }

                _goalVelocity.x = (_goalX - framePosition.x) / loopDelta;

                SetVelocity(_goalVelocity);
                return;
            }

            Vector2 _extraForce = new(0, grounded ? 0 : -Data.ExtraConstantGravity * (endedJumpEarly && Velocity.y > 0 ? Data.EndJumpEarlyExtraForceMultiplier : 1));
            forceConstant.force = _extraForce * rb.mass;

            float _targetSpeed = hasInputThisFrame ? Data.BaseSpeed : 0;

            if (IsCrouching) {
                float crouchPoint = Mathf.InverseLerp(0, Data.CrouchSlowDownTime, loopTime - timeStartedCrouching);
                _targetSpeed *= Mathf.Lerp(1, Data.CrouchSpeedModifier, crouchPoint);
            }

            float _step = hasInputThisFrame ? Data.Acceleration : Data.Friction;
            Vector2 _xDir = hasInputThisFrame ? frameDirection : Velocity.normalized;

            if (Vector3.Dot(trimmedFrameVelocity, frameDirection) < 0) {
                _step *= Data.DirectionCorrectionMultiplier;
            }

            Vector2 _newVelocity;
            _step *= loopDelta;

            if (grounded) {
                float _speed = Mathf.MoveTowards(Velocity.magnitude, _targetSpeed, _step);

                Vector2 _targetVelocity = _xDir * _speed;
                float _newSpeed = Mathf.MoveTowards(Velocity.magnitude, _targetVelocity.magnitude, _step);

                Vector2 _smoothed = Vector2.MoveTowards(Velocity, _targetVelocity, _step); // Smooth but potentially inaccurate
                Vector2 _direct = _targetVelocity.normalized * _newSpeed; // Accurate but abrupt
                float _slopePoint = Mathf.InverseLerp(0, SLOPE_ANGLE_FOR_EXACT_MOVEMENT, Mathf.Abs(frameDirection.y));

                // Calculate the blended velocity
                _newVelocity = Vector2.Lerp(_smoothed, _direct, _slopePoint);

            } else {
                _step *= Data.AirFrictionMultiplier;

                if (wallJumpInputNerfPoint < 1 && (int)Mathf.Sign(_xDir.x) == (int)Mathf.Sign(wallDirectionForJump)) {
                    if (loopTime < returnWallInputLossAfter) _xDir.x = -wallDirectionForJump;
                    else _xDir.x *= wallJumpInputNerfPoint;
                }

                float _targetX = Mathf.MoveTowards(trimmedFrameVelocity.x, _xDir.x * _targetSpeed, _step);
                _newVelocity = new Vector2(_targetX, rb.linearVelocity.y);
            }

            SetVelocity((_newVelocity + AdditionalFrameVelocities()) * currentFrameSpeedModifier);

            Vector2 AdditionalFrameVelocities() {
                if (immediateMove.sqrMagnitude > SKIN_WIDTH) {
                    rb.MovePosition(framePosition + immediateMove);
                }

                totalTransientVelocityAppliedLastFrame = frameTransientVelocity + decayingTransientVelocity;
                return totalTransientVelocityAppliedLastFrame;
            }
        }

        private void SetVelocity(Vector2 _velocity) {
            rb.linearVelocity = _velocity;
            Velocity = _velocity;
        }

        private void SaveCharacterState() {
            CurrentState = new MovementState() {
                position = framePosition,
                rotation = rb.rotation,
                velocity = Velocity,
                isGrounded = grounded
            };
        }
        #endregion

        // =================================================

        #region External Triggers
        private void CalculateExternalModifiers() {
            frameSpeedModifier = Vector2.one;
            
            foreach (ISpeedModifier _modifier in modifiers) {
                if ((_modifier.OnGround && grounded) || (_modifier.InAir && !grounded)) {
                    frameSpeedModifier += _modifier.Modifier;
                }
            }

            currentFrameSpeedModifier = Vector2.SmoothDamp(currentFrameSpeedModifier, frameSpeedModifier, ref frameSpeedModifierVelocity, 0.1f);
        }
        #endregion
    }
}