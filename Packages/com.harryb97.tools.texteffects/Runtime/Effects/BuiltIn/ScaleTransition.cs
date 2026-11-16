using System;
using HarryTools.SerializeReferences;
using UnityEngine;

namespace HarryTools.TextEffects {
    /// <summary> Scales the character around its center while it appears or disappears </summary>
    [Serializable, TypeMenu("Transition/Scale")]
    public sealed class ScaleTransition : TextEffect {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private TransitionDirection direction = TransitionDirection.In;
        [Tooltip("Scale at the hidden end of the transition"), SerializeField] private float from = 0f;
        #endregion

        public override EffectKind Kind => direction == TransitionDirection.In ? EffectKind.Appear : EffectKind.Disappear;

        // ==================== METHODS ====================
        public override void Apply(ref CharacterState _char, in EffectTime _time) {
            float eased = Eased(_time.Progress);
            float shown = direction == TransitionDirection.In ? eased : 1f - eased;
            _char.ScaleAroundCenter(Mathf.LerpUnclamped(from, 1f, shown));
        }
    }
}
