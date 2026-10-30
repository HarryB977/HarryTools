using System;
using UnityEngine;

using HarryTools.SerializeReferences;

namespace HarryTools.Conditions.Variants {
    [Serializable, TypeMenu("Components/Animator Is In State")]
    public class AnimatorIsInState : Condition {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private TargetComponent<Animator> animator = new();
        [SerializeField] private string stateName;
        [SerializeField, Min(0)] private int layer;
        #endregion

        // ==================== METHODS ====================
        protected override bool Evaluate(LogicContext _context) {
            Animator _animator = animator.Resolve(_context);
            if (_animator == null || !_animator.isActiveAndEnabled) return false;

            return _animator.GetCurrentAnimatorStateInfo(layer).IsName(stateName);
        }
    }
}
