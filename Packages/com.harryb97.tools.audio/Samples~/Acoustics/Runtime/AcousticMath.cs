using UnityEngine;

namespace HarryTools.Audio.Acoustics {
    /// <summary> Pure acoustic formulas. Bands are Vector3 (x = Low, y = Mid, z = High) holding linear energy gains from 0 to 1. </summary>
    internal static class AcousticMath {
        // ==================== VARIABLES ===================
        private const float Epsilon = 1e-6f;

        // ==================== PUBLIC METHODS ===================
        /// <summary> Energy that survives a wall of the given thickness: T^(thickness / ReferenceThickness) per band. </summary>
        public static Vector3 Transmit(Vector3 _transmission, float _thickness) {
            if (!(_thickness > 0f)) return Vector3.one;
            float _exponent = _thickness / AcousticMaterial.ReferenceThickness;
            return new Vector3(
                Mathf.Pow(Mathf.Clamp01(_transmission.x), _exponent),
                Mathf.Pow(Mathf.Clamp01(_transmission.y), _exponent),
                Mathf.Pow(Mathf.Clamp01(_transmission.z), _exponent));
        }

        /// <summary> Blends the open (unobstructed) fraction with the energy that gets through the walls. </summary>
        public static Vector3 Occlude(float _openFraction, Vector3 _throughWalls) {
            float _open = float.IsNaN(_openFraction) ? 0f : Mathf.Clamp01(_openFraction);
            float _closed = 1f - _open;
            return new Vector3(
                _open + _closed * _throughWalls.x,
                _open + _closed * _throughWalls.y,
                _open + _closed * _throughWalls.z);
        }

        /// <summary> Air absorption over a distance. Low is not attenuated. </summary>
        public static Vector3 Air(float _distance, float _midDbPerMeter, float _highDbPerMeter) {
            if (!(_distance > 0f)) return Vector3.one;
            return new Vector3(
                1f,
                Mathf.Pow(10f, -_midDbPerMeter * _distance / 10f),
                Mathf.Pow(10f, -_highDbPerMeter * _distance / 10f));
        }

        /// <summary> Turns band gains into volume and filter cutoffs. Cutoffs interpolate in log frequency between the neutral value (ratio 1) and the limit (ratio 0). </summary>
        public static AcousticResult ToResult(Vector3 _bandGains, float _floorDb, float _minLowPass, float _maxHighPass) {
            float _floorVolume = Mathf.Pow(10f, _floorDb / 20f);
            bool _valid = IsFinite(_bandGains.x) && IsFinite(_bandGains.y) && IsFinite(_bandGains.z);

            float _low = _valid ? Mathf.Max(0f, _bandGains.x) : 0f;
            float _mid = _valid ? Mathf.Max(0f, _bandGains.y) : 0f;
            float _high = _valid ? Mathf.Max(0f, _bandGains.z) : 0f;

            float _lowPassRatio = _mid > Epsilon ? Mathf.Clamp01(_high / _mid) : 0f;
            float _highPassRatio = _mid > Epsilon ? Mathf.Clamp01(_low / _mid) : 0f;

            AcousticResult _result = AcousticResult.Neutral;
            _result.VolumeMultiplier = Mathf.Max(Mathf.Sqrt(Mathf.Min(_mid, 1f)), _floorVolume);
            _result.LowPassCutoff = Mathf.Max(LogLerp(_minLowPass, AcousticResult.NeutralLowPass, _lowPassRatio), _minLowPass);
            _result.HighPassCutoff = Mathf.Min(LogLerp(_maxHighPass, AcousticResult.NeutralHighPass, _highPassRatio), _maxHighPass);
            return _result;
        }

        // ==================== PRIVATE METHODS ===================
        private static float LogLerp(float _a, float _b, float _t) {
            float _la = Mathf.Log(Mathf.Max(_a, 1f));
            float _lb = Mathf.Log(Mathf.Max(_b, 1f));
            return Mathf.Exp(_la + (_lb - _la) * _t);
        }

        private static bool IsFinite(float _value) {
            return !float.IsNaN(_value) && !float.IsInfinity(_value);
        }
    }
}
