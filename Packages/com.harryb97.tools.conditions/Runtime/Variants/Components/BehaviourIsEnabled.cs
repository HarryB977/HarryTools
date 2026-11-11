using System;
using UnityEngine;

using HarryTools.SerializeReferences;

namespace HarryTools.Conditions.Variants {
    [Serializable, TypeMenu("Components/Behaviour Is Enabled")]
    public class BehaviourIsEnabled : Condition {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private TargetComponent<Behaviour> behaviour = new();
        [Tooltip("The GameObject must also be active"), SerializeField] private bool activeAndEnabled = true;
        #endregion

        // ==================== METHODS ====================
        protected override bool Evaluate(LogicContext _context) {
            Behaviour _behaviour = behaviour.Resolve(_context);
            if (_behaviour == null) return false;
            return activeAndEnabled ? _behaviour.isActiveAndEnabled : _behaviour.enabled;
        }
    }
}
