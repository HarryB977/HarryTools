using System;
using UnityEngine;

using HarryTools.SerializeReferences;

namespace HarryTools.Conditions.Variants {
    /// <summary> Invierte el resultado de otra condición </summary>
    [Serializable, TypeMenu("Logic/Not", -97)]
    public class NotCondition : ICondition {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeReference, TypeSelector] private ICondition condition;
        #endregion

        // ==================== INTERFACES ====================
        public bool Check(LogicContext _context) => condition == null || !condition.Check(_context);
    }
}
