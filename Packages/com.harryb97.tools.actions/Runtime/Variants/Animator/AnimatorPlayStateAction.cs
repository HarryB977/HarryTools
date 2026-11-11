using System;
using UnityEngine;

using HarryTools.Conditions;
using HarryTools.SerializeReferences;

namespace HarryTools.Actions.Variants {
    [Serializable, TypeMenu("Animator/Play State")]
    public class AnimatorPlayStateAction : InstantAction {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private TargetComponent<Animator> animator = new();
        [SerializeField] private string stateName;
        [SerializeField] private int layer = -1;
        [Tooltip("Transition duration in seconds (0 = instant change)"), SerializeField, Min(0)] private float crossFade;
        #endregion

        // ==================== METHODS ====================
        protected override void Execute(LogicContext _context) {
            Animator _animator = animator.Resolve(_context);
            if (_animator == null || string.IsNullOrEmpty(stateName)) return;

            if (crossFade > 0) _animator.CrossFadeInFixedTime(stateName, crossFade, layer);
            else _animator.Play(stateName, layer);
        }
    }
}
