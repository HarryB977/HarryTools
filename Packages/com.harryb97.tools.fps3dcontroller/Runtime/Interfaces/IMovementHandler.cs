using UnityEngine;

namespace HarryTools.FPS3DController {
    public interface IMovementHandler {
        // Inputs
        Vector2 MovementInput { get; }

        bool JumpHeld { get; }
        bool CrouchHeld { get; }

        // Time.time of the last press, float.NegativeInfinity if never pressed
        float LastJumpPressTime { get; }
        float LastDashPressTime { get; }

        // References
        Transform Transform { get; }
        Transform Orientation { get; }
        Rigidbody Rigidbody { get; }
        CapsuleCollider Collider { get; }

        // Data
        MovementController.Data Data { get; }
    }
}
