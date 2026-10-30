using System;
using UnityEngine;

using HarryTools.SerializeReferences;

using Object = UnityEngine.Object;

namespace HarryTools.Conditions.Variants {
    /// <summary> El objeto está asignado y no ha sido destruido </summary>
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
