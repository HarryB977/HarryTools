using System;
using UnityEngine;

using HarryTools.Conditions;
using HarryTools.SerializeReferences;

namespace HarryTools.Actions.Variants {
    [Serializable, TypeMenu("Animator/Set Parameter")]
    public class AnimatorSetParameterAction : InstantAction {
        public enum ParameterType {
            Trigger,
            ResetTrigger,
            Bool,
            Float,
            Integer
        }

        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private TargetComponent<Animator> animator = new();
        [SerializeField] private string parameter;
        [SerializeField] private ParameterType type = ParameterType.Trigger;
        [Space]
        [Tooltip("Solo para 'Bool'"), SerializeField] private bool boolValue;
        [Tooltip("Solo para 'Float' e 'Integer'"), SerializeField] private float numberValue;
        #endregion

        // ==================== METHODS ====================
        protected override void Execute(LogicContext _context) {
            Animator _animator = animator.Resolve(_context);
            if (_animator == null || string.IsNullOrEmpty(parameter)) return;

            switch (type) {
                case ParameterType.Trigger: _animator.SetTrigger(parameter); break;
                case ParameterType.ResetTrigger: _animator.ResetTrigger(parameter); break;
                case ParameterType.Bool: _animator.SetBool(parameter, boolValue); break;
                case ParameterType.Float: _animator.SetFloat(parameter, numberValue); break;
                case ParameterType.Integer: _animator.SetInteger(parameter, Mathf.RoundToInt(numberValue)); break;
            }
        }
    }
}
