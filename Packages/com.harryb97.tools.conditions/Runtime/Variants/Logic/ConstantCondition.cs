using System;
using UnityEngine;

using HarryTools.SerializeReferences;

namespace HarryTools.Conditions.Variants {
    [Serializable, TypeMenu("Logic/Constant", -100)]
    public class ConstantCondition : ICondition {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private bool value = true;
        #endregion

        // ==================== INTERFACES ====================
        public bool Check(LogicContext _context) => value;
    }
}
