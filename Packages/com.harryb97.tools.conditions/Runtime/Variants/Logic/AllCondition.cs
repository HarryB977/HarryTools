using System;
using UnityEngine;

using HarryTools.SerializeReferences;

namespace HarryTools.Conditions.Variants {
    /// <summary> Every condition passes (AND). Useful to nest groups inside an 'Any' list </summary>
    [Serializable, TypeMenu("Logic/All (AND)", -99)]
    public class AllCondition : Condition {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeReference, TypeSelector] private ICondition[] conditions = Array.Empty<ICondition>();
        #endregion

        // ==================== METHODS ====================
        protected override bool Evaluate(LogicContext _context) => conditions.CheckAll(_context);
    }
}
