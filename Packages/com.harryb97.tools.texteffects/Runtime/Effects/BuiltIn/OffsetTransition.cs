using System;
using HarryTools.SerializeReferences;
using UnityEngine;

namespace HarryTools.TextEffects {
    /// <summary> Moves the character from or to an offset while it appears or disappears </summary>
    [Serializable, TypeMenu("Transition/Offset")]
    public sealed class OffsetTransition : TextEffect {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private TransitionDirection direction = TransitionDirection.In;
        [Tooltip("Offset at the hidden end of the transition, in units of the character size"), SerializeField]
        private Vector3 offset = new(0f, -0.5f, 0f);
        #endregion

        public override EffectKind Kind => direction == TransitionDirection.In ? EffectKind.Appear : EffectKind.Disappear;

        // ==================== METHODS ====================
        public override void Apply(ref CharacterState _char, in EffectTime _time) {
            float eased = Eased(_time.Progress);
            float hidden = direction == TransitionDirection.In ? 1f - eased : eased;
            _char.Translate(offset * (_char.Size * hidden));
        }
    }
}
