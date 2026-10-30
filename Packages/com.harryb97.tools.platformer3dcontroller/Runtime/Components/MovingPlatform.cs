using UnityEngine;

namespace HarryTools.Platformer3DController {
    /// <summary>
    /// Marks a transform as a platform that carries the player. The MovementController follows its
    /// position and yaw while standing on any of its child colliders, whatever moves it (animation, script or kinematic rigidbody).
    /// </summary>
    [DisallowMultipleComponent]
    public class MovingPlatform : MonoBehaviour { }
}
