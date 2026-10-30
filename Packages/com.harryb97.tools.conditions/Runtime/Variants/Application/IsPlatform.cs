using System;
using UnityEngine;

using HarryTools.SerializeReferences;

namespace HarryTools.Conditions.Variants {
    [Serializable, TypeMenu("Application/Is Platform")]
    public class IsPlatform : Condition {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private RuntimePlatform[] platforms = Array.Empty<RuntimePlatform>();
        #endregion

        // ==================== METHODS ====================
        protected override bool Evaluate(LogicContext _context) => Array.IndexOf(platforms, Application.platform) >= 0;
    }
}
