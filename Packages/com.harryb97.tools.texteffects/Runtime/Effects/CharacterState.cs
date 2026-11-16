using UnityEngine;

namespace HarryTools.TextEffects {
    /// <summary> Mesh data of one character that effects read and modify </summary>
    public struct CharacterState {
        // ==================== VARIABLES ===================
        #region Public Variables
        public Vector3 BottomLeft;
        public Vector3 TopLeft;
        public Vector3 TopRight;
        public Vector3 BottomRight;

        public Color32 ColorBL;
        public Color32 ColorTL;
        public Color32 ColorTR;
        public Color32 ColorBR;

        /// <summary> Index of the character in the text </summary>
        public int Index;

        /// <summary> Index of the character inside the span that applies the effect </summary>
        public int IndexInSpan;

        /// <summary> Number of characters of the span that applies the effect </summary>
        public int SpanLength;

        /// <summary> Height of the character in mesh units </summary>
        public float Size;

        public readonly Vector3 Center => (BottomLeft + TopLeft + TopRight + BottomRight) * 0.25f;
        #endregion

        // ==================== METHODS ====================
        #region Transform
        public void Translate(Vector3 _offset) {
            BottomLeft += _offset;
            TopLeft += _offset;
            TopRight += _offset;
            BottomRight += _offset;
        }

        public void ScaleAroundCenter(float _scale) {
            Vector3 center = Center;
            BottomLeft = center + (BottomLeft - center) * _scale;
            TopLeft = center + (TopLeft - center) * _scale;
            TopRight = center + (TopRight - center) * _scale;
            BottomRight = center + (BottomRight - center) * _scale;
        }

        /// <summary> Rotates around the center on the XY plane. Positive degrees turn counter-clockwise </summary>
        public void RotateAroundCenter(float _degrees) {
            Vector3 center = Center;
            float radians = _degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            BottomLeft = Rotate(BottomLeft, center, cos, sin);
            TopLeft = Rotate(TopLeft, center, cos, sin);
            TopRight = Rotate(TopRight, center, cos, sin);
            BottomRight = Rotate(BottomRight, center, cos, sin);
        }

        private static Vector3 Rotate(Vector3 _point, Vector3 _center, float _cos, float _sin) {
            float x = _point.x - _center.x;
            float y = _point.y - _center.y;
            return new Vector3(_center.x + x * _cos - y * _sin, _center.y + x * _sin + y * _cos, _point.z);
        }
        #endregion

        #region Color
        public void MultiplyAlpha(float _factor) {
            ColorBL.a = ScaleAlpha(ColorBL.a, _factor);
            ColorTL.a = ScaleAlpha(ColorTL.a, _factor);
            ColorTR.a = ScaleAlpha(ColorTR.a, _factor);
            ColorBR.a = ScaleAlpha(ColorBR.a, _factor);
        }

        /// <summary> Blends the color toward the target and keeps the current alpha </summary>
        public void LerpColor(Color32 _target, float _t) {
            ColorBL = LerpKeepAlpha(ColorBL, _target, _t);
            ColorTL = LerpKeepAlpha(ColorTL, _target, _t);
            ColorTR = LerpKeepAlpha(ColorTR, _target, _t);
            ColorBR = LerpKeepAlpha(ColorBR, _target, _t);
        }

        private static byte ScaleAlpha(byte _alpha, float _factor) {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(_alpha * _factor), 0, 255);
        }

        private static Color32 LerpKeepAlpha(Color32 _from, Color32 _to, float _t) {
            Color32 result = Color32.Lerp(_from, _to, _t);
            result.a = _from.a;
            return result;
        }
        #endregion
    }
}
