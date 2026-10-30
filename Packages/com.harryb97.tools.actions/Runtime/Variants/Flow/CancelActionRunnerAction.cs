using System;
using UnityEngine;

using HarryTools.Conditions;
using HarryTools.SerializeReferences;

namespace HarryTools.Actions.Variants {
    [Serializable, TypeMenu("Flow/Cancel Action Runner", -91)]
    public class CancelActionRunnerAction : InstantAction {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private ActionRunner runner;
        #endregion

        // ==================== METHODS ====================
        protected override void Execute(LogicContext _context) {
            if (runner != null) runner.Cancel();
        }
    }
}
