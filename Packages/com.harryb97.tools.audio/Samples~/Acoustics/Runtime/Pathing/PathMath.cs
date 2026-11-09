using UnityEngine;

namespace HarryTools.Audio.Acoustics {
    /// <summary> Pure formulas that combine the direct path and the path around obstacles. </summary>
    internal static class PathMath {
        // ==================== WEIGHT ===================
        /// <summary> 1 with no open rays, 0 once the open fraction reaches <paramref name="_maxOpen"/>, smooth in between. </summary>
        public static float PathWeight(float _open, float _maxOpen) {
            if (!(_maxOpen > 0f)) return 0f;
            float _t = Mathf.Clamp01(_open / _maxOpen);
            return 1f - Mathf.SmoothStep(0f, 1f, _t);
        }

        // ==================== LOSS ===================
        /// <summary> Energy bands lost by the detour (path length minus direct distance, never negative). Low is not attenuated. </summary>
        public static Vector3 BendLoss(float _excess, float _midDbPerMeter, float _highDbPerMeter) {
            if (!(_excess > 0f)) return Vector3.one;
            return new Vector3(
                1f,
                Mathf.Pow(10f, -_midDbPerMeter * _excess / 10f),
                Mathf.Pow(10f, -_highDbPerMeter * _excess / 10f));
        }

        // ==================== SHARE ===================
        /// <summary> Part of the mid band energy that arrives through the path. 0 when there is no energy. </summary>
        public static float PathShare(float _directMid, float _pathMid) {
            float _total = _directMid + _pathMid;
            if (!(_total > 0f)) return 0f;
            return _pathMid / _total;
        }

        // ==================== OUTPUT ===================
        /// <summary> Sums both absolute energies per band and divides by the squared perceived roll-off, capped at 1 per band. </summary>
        public static Vector3 Output(Vector3 _directEnergy, Vector3 _pathEnergy, float _perceivedRoll) {
            if (!(_perceivedRoll > 0f)) return Vector3.one;
            float _scale = 1f / (_perceivedRoll * _perceivedRoll);
            Vector3 _sum = (_directEnergy + _pathEnergy) * _scale;
            return new Vector3(Mathf.Min(1f, _sum.x), Mathf.Min(1f, _sum.y), Mathf.Min(1f, _sum.z));
        }

        // ==================== FLOOR ===================
        /// <summary> Floor in dB corrected so it stays relative to a sound with no acoustics. </summary>
        public static float FloorDb(float _floorDb, float _directRoll, float _perceivedRoll) {
            if (!(_directRoll > 0f) || !(_perceivedRoll > 0f)) return _floorDb;
            return _floorDb + 20f * Mathf.Log10(_directRoll / _perceivedRoll);
        }
    }
}
