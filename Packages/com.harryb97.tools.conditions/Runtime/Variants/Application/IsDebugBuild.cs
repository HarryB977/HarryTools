using System;
using UnityEngine;

using HarryTools.SerializeReferences;

namespace HarryTools.Conditions.Variants {
    /// <summary> Passes in the editor and in development builds </summary>
    [Serializable, TypeMenu("Application/Is Debug Build")]
    public class IsDebugBuild : Condition {
        // ==================== METHODS ====================
        protected override bool Evaluate(LogicContext _context) => Debug.isDebugBuild;
    }
}
