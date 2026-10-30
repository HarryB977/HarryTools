using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace HarryTools.Platformer3DController.Sample {
    /// <summary> Keyboard and mouse reading that works with the Input System package and with the legacy Input Manager. </summary>
    public static class SampleInput {
        // ==================== METHODS ====================
        public static Vector2 Movement {
            get {
#if ENABLE_INPUT_SYSTEM
                Keyboard _keyboard = Keyboard.current;
                if (_keyboard == null) return Vector2.zero;

                Vector2 _input = Vector2.zero;
                if (_keyboard.aKey.isPressed || _keyboard.leftArrowKey.isPressed) _input.x -= 1f;
                if (_keyboard.dKey.isPressed || _keyboard.rightArrowKey.isPressed) _input.x += 1f;
                if (_keyboard.sKey.isPressed || _keyboard.downArrowKey.isPressed) _input.y -= 1f;
                if (_keyboard.wKey.isPressed || _keyboard.upArrowKey.isPressed) _input.y += 1f;
                return _input;
#else
                return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif
            }
        }

        /// <summary> Mouse delta in pixels for this frame. </summary>
        public static Vector2 Look {
            get {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
#else
                // The legacy axes are already scaled, 20 makes them roughly match the pixel delta
                return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 20f;
#endif
            }
        }

        public static float Zoom {
            get {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current != null ? Mouse.current.scroll.ReadValue().y / 120f : 0f;
#else
                return Input.mouseScrollDelta.y;
#endif
            }
        }

        public static bool JumpPressed {
            get {
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.Space);
#endif
            }
        }

        public static bool SprintHeld {
            get {
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
#else
                return Input.GetKey(KeyCode.LeftShift);
#endif
            }
        }

        public static bool UnlockCursorPressed {
            get {
#if ENABLE_INPUT_SYSTEM
                return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
                return Input.GetKeyDown(KeyCode.Escape);
#endif
            }
        }

        public static bool LockCursorPressed {
            get {
#if ENABLE_INPUT_SYSTEM
                return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
                return Input.GetMouseButtonDown(0);
#endif
            }
        }
    }
}
