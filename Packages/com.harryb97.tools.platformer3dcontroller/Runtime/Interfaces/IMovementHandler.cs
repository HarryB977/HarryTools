using UnityEngine;

namespace HarryTools.Platformer3DController {
    public interface IMovementHandler {
        // Inputs
        Vector2 MovementInput { get; }

        bool SprintHeld { get; }

        // Time.time of the last press, float.NegativeInfinity if never pressed
        float LastJumpPressTime { get; }

        // References
        // Movement input is relative to this transform's yaw (usually the camera)
        Transform Orientation { get; }
        Rigidbody Rigidbody { get; }

        // Data
        MovementController.Data Data { get; }
    }
}
