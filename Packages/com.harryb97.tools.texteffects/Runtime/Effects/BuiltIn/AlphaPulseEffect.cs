using System;
using HarryTools.SerializeReferences;
using UnityEngine;

namespace HarryTools.TextEffects {
    /// <summary> Pulses the opacity of the characters </summary>
    [Serializable, TypeMenu("Continuous/Alpha Pulse")]
    public sealed class AlphaPulseEffect : TextEffect {
        // ==================== VARIABLES ===================
        #region Private Variables
        [Tooltip("Lowest opacity factor reached"), SerializeField, Range(0f, 1f)] private float min = 0.4f;
        [Tooltip("Cycles per second"), SerializeField] private float frequency = 1.5f;
        #endregion

        public override EffectKind Kind => EffectKind.Continuous;

        // ==================== METHODS ====================
        public override void Apply(ref CharacterState _char, in EffectTime _time) {
            float wave = 0.5f + 0.5f * Mathf.Sin(_time.GlobalTime * frequency * Mathf.PI * 2f);
            _char.MultiplyAlpha(Mathf.Lerp(min, 1f, wave));
        }
    }
}
