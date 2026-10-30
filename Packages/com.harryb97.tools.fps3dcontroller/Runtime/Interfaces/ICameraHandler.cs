using UnityEngine;

namespace HarryTools.FPS3DController {
    public interface ICameraHandler {
        // Inputs
        Vector2 MovementInput { get; }

        // Raw mouse delta in pixels for this frame
        Vector2 LookInput { get; }

        // References
        Transform Head { get; }
        Camera Camera { get; }

        MovementController Movement { get; }

        // Data
        CameraController.Data Data { get; }
    }
}
