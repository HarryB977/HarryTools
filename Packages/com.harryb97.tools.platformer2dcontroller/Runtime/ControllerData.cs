using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

using HarryUtils;

using Logger = HarryTools.LoggerTool.Logger;

namespace HarryTools.Platformer2DController {
    public abstract class ControllerData {
        #region Size
        public struct GeneratedSize {
            // Standing
            public float Height;
            public float Width;
            public float StepHeight;
            public float RayInset;

            public Vector2 StandingColliderSize;
            public Vector2 StandingColliderCenter;

            // Crouching
            public float CrouchingHeight;

            public Vector2 CrouchColliderSize;
            public Vector2 CrouchingColliderCenter;
        }
        
        [Serializable]
        public class Size {
            public const float STEP_BUFFER = 0.05f;
            public const float COLLIDER_EDGE_RADIUS = 0.05f;

            [Range(0.1f, 10)] public float Height = 1.8f;
            [Range(0.1f, 10)] public float Width = 0.6f;
            [Range(STEP_BUFFER, 15)] public float StepHeight = 0.5f;
            [Range(0.1f, 10)] public float CrouchHeight = 0.6f;
            [Range(0.01f, 0.2f)] public float RayInset = 0.1f;

            public GeneratedSize GenerateCharacterSize() {
                ValidateHeights();

                GeneratedSize _generatedSize = new() {
                    Height = Height,
                    Width = Width,
                    StepHeight = StepHeight,
                    RayInset = RayInset
                };

                _generatedSize.StandingColliderSize = new Vector2(_generatedSize.Width - COLLIDER_EDGE_RADIUS * 2, _generatedSize.Height - _generatedSize.StepHeight - COLLIDER_EDGE_RADIUS * 2);
                _generatedSize.StandingColliderCenter = new Vector2(0, _generatedSize.Height - _generatedSize.StandingColliderSize.y / 2 - COLLIDER_EDGE_RADIUS);

                _generatedSize.CrouchingHeight = CrouchHeight;
                _generatedSize.CrouchColliderSize = new Vector2(_generatedSize.Width - COLLIDER_EDGE_RADIUS * 2, _generatedSize.CrouchingHeight - _generatedSize.StepHeight);
                _generatedSize.CrouchingColliderCenter = new Vector2(0, _generatedSize.CrouchingHeight - _generatedSize.CrouchColliderSize.y / 2 - COLLIDER_EDGE_RADIUS);

                return _generatedSize;
            }

            private static double _lastDebugLogTime;
            private const double TIME_BETWEEN_LOGS = 1f;

            private void ValidateHeights() {
#if UNITY_EDITOR
                float _maxStepHeight = Height - STEP_BUFFER;
                if (StepHeight > _maxStepHeight) {
                    StepHeight = _maxStepHeight;
                    Log("Step height cannot be larger than height");
                }

                float _minCrouchHeight = StepHeight + STEP_BUFFER;
                if (CrouchHeight < _minCrouchHeight) {
                    CrouchHeight = _minCrouchHeight;
                    Log("Crouch height must be larger than step height");
                }

                void Log(string _log) {
                    double _time = EditorApplication.timeSinceStartup;
                    if (_lastDebugLogTime + TIME_BETWEEN_LOGS > _time) return;

                    _lastDebugLogTime = _time;
                    Logger.LogWarning("ControllerData", _log);
                }
    #endif
            }
        }
        #endregion

        public enum PositionCorrectionMode {
            Velocity, Immediate
        }
        
        // ==================== VARIABLES ===================
        #region Properties
        // Layers
        public LayerMask PlayerLayerMask => LayerMask.NameToLayer(playerLayerMask);
        public LayerMask ClimbLayerMask => LayerMask.NameToLayer(climbLayerMask);
        public LayerMask LadderLayerMask => LayerMask.NameToLayer(ladderLayerMask);

        public LayerMask CollisionLayerMask {
            get {
                LayerMask _layerMask = default;

                if (collisionLayersMask.IsNullOrEmpty()) {
                    return _layerMask;
                }

                for (int i = 0; i < collisionLayersMask.Length; i++) {
                    int _layerNumber = LayerMask.NameToLayer(collisionLayersMask[i]);

                    if (_layerNumber != -1) _layerMask |= 1 << _layerNumber;
                    else Logger.LogWarning("ControllerData", $"Layer {collisionLayersMask[i]} does not exists");
                }

                return _layerMask;
            }
        }
        #endregion
        
        #region Private Variables
        [Header("Layers")]
        public string playerLayerMask;
        public string climbLayerMask;
        public string ladderLayerMask;
        [Space]
        public string[] collisionLayersMask;

        [Header("Settings")]
        public PositionCorrectionMode PositionCorrection = PositionCorrectionMode.Velocity;
        [Space]
        public float VerticalDeadZoneThreshold = 0.3f;
        public float HorizontalDeadZoneThreshold = 0.1f;
        [Space]
        public Size ControllerSize;

        [Header("Movement")]
        public float BaseSpeed = 9f;
        public float Acceleration = 50f;
        public float Friction = 30f;
        public float AirFrictionMultiplier = 0.5f;
        public float DirectionCorrectionMultiplier = 3f;
        public float MaxWalkableSlope = 50f;

        [Header("Jump")]
        public float ExtraConstantGravity = 40f;
        public float BufferedJumpTime = 0.15f;
        public float CoyoteTime = 0.15f;
        public float JumpPower = 20f;
        public float EndJumpEarlyExtraForceMultiplier = 3f;
        [Space]
        public int MaxAirJumps = 1;

        [Header("Dash")]
        public bool AllowDash = false;
        [Space]
        public float DashVelocity = 50;
        public float DashDuration = 0.2f;
        public float DashCooldown = 1.5f;
        public float DashEndHorizontalMultiplier = 0.5f;

        [Header("Crouch")]
        public bool AllowCrouching = false;
        [Space]
        public float CrouchSlowDownTime = 0.5f;
        public float CrouchSpeedModifier = 0.5f;

        [Header("Walls")]
        public bool AllowWallJump;
        public bool RequireInputPush;
        [Space]
        public Vector2 WallJumpPower = new(25, 15);
        public Vector2 WallPushPower = new(15, 10);
        [Space]
        public float WallJumpTotalInputLossTime = 0.2f;
        public float WallJumpInputLossReturnTime = 0.5f;
        [Space]
        public float WallClimbSpeed = 5;
        public float WallFallAcceleration = 20;
        public float WallPopForce = 10;
        public float WallCoyoteTime = 0.3f;
        public float WallDetectorRange = 0.1f;

        [Header("Ladders")]
        public bool AllowLadders;
        public bool AutoAttachToLadders = true;
        public bool SnapToLadders = true;
        [Space]
        public double LadderCooldownTime = 0.15f;
        public float LadderSnapTime = 0.02f;
        public float LadderPopForce = 10;
        public float LadderClimbSpeed = 8;
        public float LadderSlideSpeed = 12;
        public float LadderShimmySpeedMultiplier = 0.5f;

        [Header("Moving Platforms")]
        public float NegativeYVelocityNegation = 0.2f;
        public float ExternalVelocityDecayRate = 0.1f;
        #endregion
    }
}