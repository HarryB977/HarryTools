using UnityEngine;

namespace HarryTools.FPS3DController {
    public interface IMovementHandler {
        // Inputs
        Vector2 MovementInput { get; }

        bool JumpInput { get; }
        bool CrouchInput { get; }
        bool SprintInput { get; }

        // References
        Transform Transform { get; }
        Rigidbody Rigidbody { get; }
        CapsuleCollider Collider { get; }

        // Data
        MovementController.Data Data { get; }
    }
}