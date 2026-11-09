using UnityEngine;
using UnityEngine.InputSystem;

namespace HarryTools.Audio.Acoustics.Demo {
    /// <summary> Moves the object with WASD or the arrow keys, so the listener can walk through the house. It has no collider: walls do not stop it. </summary>
    [AddComponentMenu("HarryTools/Audio/Samples/Acoustics Demo Listener Mover")]
    public sealed class DemoListenerMover : MonoBehaviour {
        // ==================== VARIABLES ===================
        [Min(0), Tooltip("Meters per second.")]
        public float Speed = 5f;

        // ==================== UNITY ===================
        private void Update() {
            Keyboard _keyboard = Keyboard.current;
            if (_keyboard == null) return;
            float _x = (_keyboard.dKey.isPressed || _keyboard.rightArrowKey.isPressed ? 1f : 0f) - (_keyboard.aKey.isPressed || _keyboard.leftArrowKey.isPressed ? 1f : 0f);
            float _z = (_keyboard.wKey.isPressed || _keyboard.upArrowKey.isPressed ? 1f : 0f) - (_keyboard.sKey.isPressed || _keyboard.downArrowKey.isPressed ? 1f : 0f);
            Vector3 _direction = new Vector3(_x, 0f, _z);
            if (_direction.sqrMagnitude > 1f) _direction.Normalize();
            transform.position += _direction * (Speed * Time.deltaTime);
        }
    }
}
