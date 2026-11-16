using System;
using HarryTools.SerializeReferences;
using UnityEngine;

namespace HarryTools.TextEffects {
    /// <summary> Scales the character up once and settles back to its original size </summary>
    [Serializable, TypeMenu("One Shot/Punch Scale")]
    public sealed class PunchScaleEffect : TextEffect {
        // ==================== VARIABLES ===================
        #region Private Variables
        [Tooltip("Extra scale at the start of the punch"), SerializeField] private float strength = 0.3f;
        #endregion

        public override EffectKind Kind => EffectKind.OneShot;

        // ==================== METHODS ====================
        public override void Apply(ref CharacterState _char, in EffectTime _time) {
            float intensity = 1f - Eased(_time.Progress);
            _char.ScaleAroundCenter(1f + strength * intensity);
        }
    }
}
