using System;
using UnityEngine;

using HarryTools.Conditions;
using HarryTools.SerializeReferences;

namespace HarryTools.Actions.Variants {
    [Serializable, TypeMenu("Flow/Cancel Action Runner", -91)]
    public class CancelActionRunnerAction : InstantAction {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private TargetComponent<ActionRunner> runner = new();
        #endregion

        // ==================== METHODS ====================
        protected override void Execute(LogicContext _context) {
            ActionRunner _runner = runner?.Resolve(_context);
            if (_runner != null) _runner.Cancel();
        }
    }
}
