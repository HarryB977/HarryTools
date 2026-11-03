using UnityEngine;

namespace HarryTools.UI {
    /// <summary> Contraste entre colores según WCAG 2.x </summary>
    public static class ColorContrast {
        public const float AA = 4.5f;
        public const float AALargeText = 3f;

        // ==================== METHODS ====================
        /// <summary> Luminancia relativa (0 negro, 1 blanco) de un color sRGB </summary>
        public static float RelativeLuminance(Color _color) =>
            0.2126f * ToLinear(_color.r) + 0.7152f * ToLinear(_color.g) + 0.0722f * ToLinear(_color.b);

        /// <summary> Relación de contraste entre 1:1 y 21:1 </summary>
        public static float Ratio(Color _a, Color _b) {
            float _la = RelativeLuminance(_a);
            float _lb = RelativeLuminance(_b);
            return (Mathf.Max(_la, _lb) + 0.05f) / (Mathf.Min(_la, _lb) + 0.05f);
        }

        /// <summary> Cumple el nivel AA: 4.5:1 para texto normal, 3:1 para texto grande </summary>
        public static bool MeetsAA(Color _foreground, Color _background, bool _largeText = false) =>
            Ratio(_foreground, _background) >= (_largeText ? AALargeText : AA);

        private static float ToLinear(float _channel) =>
            _channel <= 0.03928f ? _channel / 12.92f : Mathf.Pow((_channel + 0.055f) / 1.055f, 2.4f);
    }
}
