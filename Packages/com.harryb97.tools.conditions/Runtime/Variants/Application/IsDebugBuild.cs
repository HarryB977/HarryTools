using System;
using UnityEngine;

using HarryTools.SerializeReferences;

namespace HarryTools.Conditions.Variants {
    /// <summary> Se cumple en el editor y en builds de desarrollo </summary>
    [Serializable, TypeMenu("Application/Is Debug Build")]
    public class IsDebugBuild : Condition {
        // ==================== METHODS ====================
        protected override bool Evaluate(LogicContext _context) => Debug.isDebugBuild;
    }
}
