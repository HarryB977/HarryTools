using System;
using System.Collections.Generic;
using PrimeTween;
using UnityEngine;

namespace HarryTools.TextEffects {
    /// <summary> Base class of the effects applied to the characters of a text </summary>
    [Serializable]
    public abstract class TextEffect {
        // ==================== VARIABLES ===================
        #region Public Variables
        public abstract EffectKind Kind { get; }

        /// <summary> Seconds the effect takes per character. Ignored by continuous effects </summary>
        public float Duration => duration;
        #endregion

        #region Protected Variables
        [Tooltip("Seconds the effect takes per character. Ignored by continuous effects"), SerializeField, Min(0f)]
        protected float duration = 0.3f;

        [Tooltip("Easing applied to the progress"), SerializeField]
        protected Ease ease = Ease.OutQuad;
        #endregion

        // ==================== METHODS ====================
        protected float Eased(float _t) => Easing.Evaluate(_t, ease);

        /// <summary> Modifies the character for the given time. Called every frame for every affected character, so it must not allocate </summary>
        public abstract void Apply(ref CharacterState _char, in EffectTime _time);

        /// <summary> Returns a copy with the given parameters applied, or this instance when there are none </summary>
        internal TextEffect CloneWithOverrides(IReadOnlyList<EffectParam> _overrides, List<TextIssue> _issues) {
            if (_overrides == null || _overrides.Count == 0) return this;

            TextEffect clone = (TextEffect)MemberwiseClone();
            for (int i = 0; i < _overrides.Count; i++) {
                EffectParam param = _overrides[i];
                if (!EffectParamBinder.TryApply(clone, param.Name, param.Value, out string error)) {
                    _issues?.Add(new TextIssue(IssueSeverity.Warning, error, 0));
                }
            }

            return clone;
        }
    }
}
