using UnityEngine;

namespace HarryTools.Platformer2DController {
    public interface ISpeedModifier {
        public bool InAir { get; }
        public bool OnGround { get; }
        
        public Vector2 Modifier { get; }
    }
}