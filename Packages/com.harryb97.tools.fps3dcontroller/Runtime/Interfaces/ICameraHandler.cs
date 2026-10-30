using UnityEngine;

namespace HarryTools.FPS3DController {
    public interface ICameraHandler {
        // Inputs
        Vector2 MovementInput { get; }
        Vector2 LookInput { get; }

        // References
        Transform Transform { get; }
        Rigidbody Rigidbody { get; }
        Transform Head { get; }
        Camera Camera { get; }

        MovementController Movement { get; }
        
        // Data
        CameraController.Data Data { get; }
    }
}