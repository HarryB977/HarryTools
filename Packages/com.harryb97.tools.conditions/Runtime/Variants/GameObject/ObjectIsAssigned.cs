using System;
using UnityEngine;

using HarryTools.SerializeReferences;

using Object = UnityEngine.Object;

namespace HarryTools.Conditions.Variants {
    /// <summary> The object is assigned and has not been destroyed </summary>
    [Serializable, TypeMenu("GameObject/Object Is Assigned")]
    public class ObjectIsAssigned : Condition {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private Object value;
        #endregion

        // ==================== METHODS ====================
        protected override bool Evaluate(LogicContext _context) => value != null;
    }
}
