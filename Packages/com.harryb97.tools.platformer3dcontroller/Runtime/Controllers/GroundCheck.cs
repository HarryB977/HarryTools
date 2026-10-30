using UnityEngine;

namespace HarryTools.Platformer3DController {
    /// <summary> Sphere cast towards the ground that ignores the colliders of its own rigidbody. </summary>
    public class GroundCheck {
        // ==================== VARIABLES ===================
        #region Public Variables
        public bool IsGrounded { get; private set; } = false;
        public RaycastHit Hit { get; private set; }

        /// <summary> Distance from the origin to the ground, or the max distance when nothing is hit. </summary>
        public float Distance { get; private set; } = 0f;

        public Vector3 Origin { get; private set; }
        public Vector3 Direction { get; private set; } = Vector3.down;
        public float Radius { get; private set; } = 0f;
        public float MaxDistance { get; private set; } = 0f;

        /// <summary> Ground point straight below the origin, or the end of the check when nothing is hit. </summary>
        public Vector3 Point => Origin + Direction * Distance;
        #endregion

        #region Private Variables
        private readonly RaycastHit[] hits = new RaycastHit[8];
        #endregion

        // ==================== METHODS ====================
        /// <summary> Returns true when a valid ground is found within _maxDistance (measured from the origin, not from the sphere edge). </summary>
        public bool Check(Vector3 _origin, Vector3 _direction, float _radius, float _maxDistance, LayerMask _mask, Rigidbody _ignored = null, float _maxGroundAngle = 90f) {
            Origin = _origin;
            Direction = _direction.normalized;
            Radius = Mathf.Max(0f, _radius);
            MaxDistance = Mathf.Max(Radius, _maxDistance);

            // The sphere stops one radius before the max distance, so its bottom reaches exactly _maxDistance
            int _count = Physics.SphereCastNonAlloc(Origin, Radius, Direction, hits, MaxDistance - Radius, _mask, QueryTriggerInteraction.Ignore);
            bool _found = TryGetClosest(_count, _ignored, _maxGroundAngle, true, out RaycastHit _hit);

            // A sphere cast misses colliders that already overlap the starting sphere, a ray does not
            if (!_found) {
                _count = Physics.RaycastNonAlloc(Origin, Direction, hits, MaxDistance, _mask, QueryTriggerInteraction.Ignore);
                _found = TryGetClosest(_count, _ignored, _maxGroundAngle, false, out _hit);
                Distance = _found ? _hit.distance : MaxDistance;

            } else {
                Distance = _hit.distance + Radius;
            }

            Hit = _hit;
            IsGrounded = _found;
            return _found;
        }

        public void ShowGizmos() {
            Gizmos.color = IsGrounded ? Color.green : Color.red;
            Gizmos.DrawLine(Origin, Point);
            Gizmos.DrawWireSphere(Point - Direction * Radius, Radius);
        }

        private bool TryGetClosest(int _count, Rigidbody _ignored, float _maxGroundAngle, bool _skipOverlaps, out RaycastHit _closest) {
            _closest = default;
            bool _found = false;

            for (int i = 0; i < _count; i++) {
                RaycastHit _hit = hits[i];

                if (_ignored != null && _hit.rigidbody == _ignored) continue;

                // Colliders overlapping the start of a sweep come back with distance 0 and no point
                if (_skipOverlaps && _hit.distance <= 0f && _hit.point == Vector3.zero) continue;
                if (Vector3.Angle(_hit.normal, -Direction) > _maxGroundAngle) continue;
                if (_found && _hit.distance >= _closest.distance) continue;

                _closest = _hit;
                _found = true;
            }

            return _found;
        }
    }
}
