using System;
using UnityEngine;

using HarryTools.SerializeReferences;

namespace HarryTools.Conditions.Variants {
    /// <summary> Se cumple al menos una condición (OR) </summary>
    [Serializable, TypeMenu("Logic/Any (OR)", -98)]
    public class AnyCondition : Condition {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeReference, TypeSelector] private ICondition[] conditions = Array.Empty<ICondition>();
        #endregion

        // ==================== METHODS ====================
        protected override bool Evaluate(LogicContext _context) => conditions.CheckAny(_context, true);
    }
}
