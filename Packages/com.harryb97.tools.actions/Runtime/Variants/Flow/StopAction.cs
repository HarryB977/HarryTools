using System;
using System.Threading;
using UnityEngine;

using HarryTools.Conditions;
using HarryTools.SerializeReferences;

using Cysharp.Threading.Tasks;

namespace HarryTools.Actions.Variants {
    /// <summary> Stops the execution: no further action of the list runs (nor of the lists containing it) </summary>
    [Serializable, TypeMenu("Flow/Stop", -90)]
    public class StopAction : IAction {
        // ==================== VARIABLES ===================
        #region Private Variables
        [Tooltip("Only stops if they are met (empty = always)"), SerializeField] private ConditionList conditions = new();
        #endregion

        // ==================== INTERFACES ====================
        public UniTask Run(LogicContext _context, CancellationToken _token) {
            if (conditions.Check(_context)) throw new OperationCanceledException("Stop");
            return UniTask.CompletedTask;
        }
    }
}
