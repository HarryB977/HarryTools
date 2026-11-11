using UnityEngine;

namespace HarryTools.Actions.Variants {
    public enum TransformProperty {
        Position,
        [Tooltip("Euler angles")] Rotation,
        Scale
    }

    public enum TransformSpace {
        Local,
        World
    }

    internal static class TransformPropertyUtils {
        // ==================== METHODS ====================
        public static Vector3 Get(this Transform _transform, TransformProperty _property, TransformSpace _space) {
            bool _local = _space == TransformSpace.Local;

            return _property switch {
                TransformProperty.Position => _local ? _transform.localPosition : _transform.position,
                TransformProperty.Rotation => _local ? _transform.localEulerAngles : _transform.eulerAngles,
                _ => _local ? _transform.localScale : _transform.lossyScale
            };
        }

        /// <summary> Scale in 'World' space cannot be assigned directly, it is always assigned in local space </summary>
        public static void Set(this Transform _transform, TransformProperty _property, TransformSpace _space, Vector3 _value) {
            bool _local = _space == TransformSpace.Local;

            switch (_property) {
                case TransformProperty.Position:
                    if (_local) _transform.localPosition = _value;
                    else _transform.position = _value;
                    break;

                case TransformProperty.Rotation:
                    if (_local) _transform.localEulerAngles = _value;
                    else _transform.eulerAngles = _value;
                    break;

                default:
                    _transform.localScale = _value;
                    break;
            }
        }
    }
}
