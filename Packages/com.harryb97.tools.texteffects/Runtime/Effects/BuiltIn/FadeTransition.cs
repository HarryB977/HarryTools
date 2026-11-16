using System;
using HarryTools.SerializeReferences;
using UnityEngine;

namespace HarryTools.TextEffects {
    /// <summary> Fades the character in or out </summary>
    [Serializable, TypeMenu("Transition/Fade")]
    public sealed class FadeTransition : TextEffect {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private TransitionDirection direction = TransitionDirection.In;
        #endregion

        public override EffectKind Kind => direction == TransitionDirection.In ? EffectKind.Appear : EffectKind.Disappear;

        // ==================== METHODS ====================
        public override void Apply(ref CharacterState _char, in EffectTime _time) {
            float eased = Eased(_time.Progress);
            _char.MultiplyAlpha(direction == TransitionDirection.In ? eased : 1f - eased);
        }
    }
}
