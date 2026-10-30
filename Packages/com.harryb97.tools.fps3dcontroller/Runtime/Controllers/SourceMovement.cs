using UnityEngine;

namespace HarryTools.FPS3DController {
    public static class SourceMovement {
        // ==================== METHODS ====================
        /// <summary> Source ground friction on the horizontal plane; vertical velocity is left untouched. </summary>
        public static Vector3 ApplyFriction(Vector3 _velocity, float _friction, float _stopSpeed, float _deltaTime) {
            Vector3 _horizontal = new(_velocity.x, 0f, _velocity.z);
            float _speed = _horizontal.magnitude;
            if (_speed < 1e-4f) return new Vector3(0f, _velocity.y, 0f);

            // Below stopSpeed the drop is computed as if at stopSpeed, so slow movement stops quickly
            float _control = Mathf.Max(_speed, _stopSpeed);
            float _newSpeed = Mathf.Max(_speed - _control * _friction * _deltaTime, 0f);

            _horizontal *= _newSpeed / _speed;
            return new Vector3(_horizontal.x, _velocity.y, _horizontal.z);
        }

        /// <summary> Source ground acceleration towards a wish direction, never past the wish speed along it. </summary>
        public static Vector3 Accelerate(Vector3 _velocity, Vector3 _wishDirection, float _wishSpeed, float _acceleration, float _deltaTime) {
            return AccelerateInternal(_velocity, _wishDirection, _wishSpeed, _wishSpeed, _acceleration, _deltaTime);
        }

        /// <summary> Source air acceleration: the speed cap applies only along the wish direction. </summary>
        public static Vector3 AirAccelerate(Vector3 _velocity, Vector3 _wishDirection, float _wishSpeed, float _wishSpeedCap, float _acceleration, float _deltaTime) {
            // Capping only the add limit while the accel step keeps the full wish speed is what lets air strafing gain speed
            return AccelerateInternal(_velocity, _wishDirection, Mathf.Min(_wishSpeed, _wishSpeedCap), _wishSpeed, _acceleration, _deltaTime);
        }

        private static Vector3 AccelerateInternal(Vector3 _velocity, Vector3 _wishDirection, float _limitSpeed, float _wishSpeed, float _acceleration, float _deltaTime) {
            float _current = Vector3.Dot(_velocity, _wishDirection);
            float _add = _limitSpeed - _current;
            if (_add <= 0f) return _velocity;

            float _accel = Mathf.Min(_acceleration * _wishSpeed * _deltaTime, _add);
            return _velocity + _accel * _wishDirection;
        }
    }
}
