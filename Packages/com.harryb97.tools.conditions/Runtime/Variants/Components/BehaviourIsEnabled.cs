using System;
using UnityEngine;

using HarryTools.SerializeReferences;

namespace HarryTools.Conditions.Variants {
    [Serializable, TypeMenu("Components/Behaviour Is Enabled")]
    public class BehaviourIsEnabled : Condition {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private Behaviour behaviour;
        [Tooltip("Además, el GameObject debe estar activo"), SerializeField] private bool activeAndEnabled = true;
        #endregion

        // ==================== METHODS ====================
        protected override bool Evaluate(LogicContext _context) {
            if (behaviour == null) return false;
            return activeAndEnabled ? behaviour.isActiveAndEnabled : behaviour.enabled;
        }
    }
}
