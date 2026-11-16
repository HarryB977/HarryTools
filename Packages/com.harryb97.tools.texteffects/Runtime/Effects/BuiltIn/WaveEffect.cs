using System;
using HarryTools.SerializeReferences;
using UnityEngine;

namespace HarryTools.TextEffects {
    /// <summary> Moves the characters up and down in a traveling wave </summary>
    [Serializable, TypeMenu("Continuous/Wave")]
    public sealed class WaveEffect : TextEffect {
        // ==================== VARIABLES ===================
        #region Private Variables
        [Tooltip("Vertical displacement, in units of the character size"), SerializeField] private float amplitude = 0.15f;
        [Tooltip("Cycles per second"), SerializeField] private float frequency = 2f;
        [Tooltip("Phase shift between consecutive characters, in cycles"), SerializeField] private float wavelength = 0.3f;
        #endregion

        public override EffectKind Kind => EffectKind.Continuous;

        // ==================== METHODS ====================
        public override void Apply(ref CharacterState _char, in EffectTime _time) {
            float phase = (_time.GlobalTime * frequency + _char.Index * wavelength) * Mathf.PI * 2f;
            _char.Translate(new Vector3(0f, Mathf.Sin(phase) * amplitude * _char.Size, 0f));
        }
    }
}
