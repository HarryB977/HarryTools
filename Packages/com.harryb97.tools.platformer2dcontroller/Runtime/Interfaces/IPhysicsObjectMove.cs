using UnityEngine;

namespace HarryTools.Platformer2DController {
    public interface IPhysicsObjectMove {
        public bool UsesBounding { get; }
        public bool RequireGrounding { get; }

        public Vector2 FramePositionDelta { get; }
        public Vector2 FramePosition { get; }
        public Vector2 Velocity { get; }
        public Vector2 TakeOffVelocity { get; }
    }
}