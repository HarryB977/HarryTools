using UnityEngine;

namespace HarryTools.UI {
    /// <summary>
    /// Tonos más claros u oscuros de un color, variando la luminosidad en el espacio OKLab
    /// (perceptualmente uniforme), sin cambiar el matiz.
    /// </summary>
    public static class ColorTones {
        /// <summary> Cambio de luminosidad (L de OKLab) por cada paso de tono </summary>
        public const float Step = 0.08f;

        public const int MinTone = -3;
        public const int MaxTone = 3;

        // ==================== METHODS ====================
        public static Color Apply(Color _color, int _tone) {
            if (_tone == 0) return _color;

            Vector3 _lab = ToOkLab(_color);
            _lab.x = Mathf.Clamp01(_lab.x + Mathf.Clamp(_tone, MinTone, MaxTone) * Step);

            Color _result = FromOkLab(_lab);
            _result.a = _color.a;
            return _result;
        }

        private static Vector3 ToOkLab(Color _c) {
            float _r = ToLinear(_c.r), _g = ToLinear(_c.g), _b = ToLinear(_c.b);

            float _l = Cbrt(0.4122214708f * _r + 0.5363325363f * _g + 0.0514459929f * _b);
            float _m = Cbrt(0.2119034982f * _r + 0.6806995451f * _g + 0.1073969566f * _b);
            float _s = Cbrt(0.0883024619f * _r + 0.2817188376f * _g + 0.6299787005f * _b);

            return new Vector3(
                0.2104542553f * _l + 0.7936177850f * _m - 0.0040720468f * _s,
                1.9779984951f * _l - 2.4285922050f * _m + 0.4505937099f * _s,
                0.0259040371f * _l + 0.7827717662f * _m - 0.8086757660f * _s);
        }

        private static Color FromOkLab(Vector3 _lab) {
            float _l = _lab.x + 0.3963377774f * _lab.y + 0.2158037573f * _lab.z;
            float _m = _lab.x - 0.1055613458f * _lab.y - 0.0638541728f * _lab.z;
            float _s = _lab.x - 0.0894841775f * _lab.y - 1.2914855480f * _lab.z;

            _l = _l * _l * _l;
            _m = _m * _m * _m;
            _s = _s * _s * _s;

            return new Color(
                ToGamma(4.0767416621f * _l - 3.3077115913f * _m + 0.2309699292f * _s),
                ToGamma(-1.2684380046f * _l + 2.6097574011f * _m - 0.3413193965f * _s),
                ToGamma(-0.0041960863f * _l - 0.7034186147f * _m + 1.7076147010f * _s));
        }

        private static float Cbrt(float _v) => _v < 0f ? -Mathf.Pow(-_v, 1f / 3f) : Mathf.Pow(_v, 1f / 3f);

        private static float ToLinear(float _c) => _c <= 0.04045f ? _c / 12.92f : Mathf.Pow((_c + 0.055f) / 1.055f, 2.4f);

        private static float ToGamma(float _c) {
            _c = Mathf.Clamp01(_c);
            return _c <= 0.0031308f ? _c * 12.92f : 1.055f * Mathf.Pow(_c, 1f / 2.4f) - 0.055f;
        }
    }
}
