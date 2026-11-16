using System;
using HarryTools.SerializeReferences;
using UnityEngine;

namespace HarryTools.TextEffects {
    /// <summary> Shakes the character continuously with deterministic noise </summary>
    [Serializable, TypeMenu("Continuous/Shake")]
    public sealed class ShakeEffect : TextEffect {
        // ==================== VARIABLES ===================
        #region Private Variables
        [Tooltip("Maximum displacement, in units of the character size"), SerializeField] private float amplitude = 0.05f;
        [Tooltip("Noise speed in cycles per second"), SerializeField] private float frequency = 20f;
        #endregion

        public override EffectKind Kind => EffectKind.Continuous;

        // ==================== METHODS ====================
        public override void Apply(ref CharacterState _char, in EffectTime _time) {
            _char.Translate(ShakeNoise.Offset(_char.Index, _time.GlobalTime * frequency, amplitude * _char.Size));
        }
    }

    /// <summary> Deterministic noise shared by the shake effects </summary>
    internal static class ShakeNoise {
        public static Vector3 Offset(int _index, float _t, float _magnitude) {
            float seed = _index * 17.31f;
            float x = Mathf.Clamp(Mathf.PerlinNoise(seed, _t) * 2f - 1f, -1f, 1f);
            float y = Mathf.Clamp(Mathf.PerlinNoise(seed + 101.7f, _t) * 2f - 1f, -1f, 1f);
            return new Vector3(x * _magnitude, y * _magnitude, 0f);
        }
    }
}
