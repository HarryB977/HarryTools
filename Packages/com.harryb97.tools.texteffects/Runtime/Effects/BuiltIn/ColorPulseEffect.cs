using System;
using HarryTools.SerializeReferences;
using UnityEngine;

namespace HarryTools.TextEffects {
    /// <summary> Pulses the color of the characters toward a target color </summary>
    [Serializable, TypeMenu("Continuous/Color Pulse")]
    public sealed class ColorPulseEffect : TextEffect {
        // ==================== VARIABLES ===================
        #region Private Variables
        [Tooltip("Color reached at the peak of the pulse"), SerializeField] private Color color = Color.red;
        [Tooltip("Cycles per second"), SerializeField] private float frequency = 1.5f;
        #endregion

        public override EffectKind Kind => EffectKind.Continuous;

        // ==================== METHODS ====================
        public override void Apply(ref CharacterState _char, in EffectTime _time) {
            float wave = 0.5f + 0.5f * Mathf.Sin(_time.GlobalTime * frequency * Mathf.PI * 2f);
            _char.LerpColor(color, wave);
        }
    }
}
