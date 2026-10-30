using System;
using System.Threading;
using UnityEngine;

using HarryTools.Conditions;
using HarryTools.SerializeReferences;

using Cysharp.Threading.Tasks;

namespace HarryTools.Actions.Variants {
    [Serializable, TypeMenu("Flow/If", -97)]
    public class IfAction : IAction {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private ConditionList conditions = new();
        [Space]
        [SerializeReference, TypeSelector] private IAction[] then = Array.Empty<IAction>();
        [SerializeReference, TypeSelector] private IAction[] otherwise = Array.Empty<IAction>();
        #endregion

        // ==================== INTERFACES ====================
        public UniTask Run(LogicContext _context, CancellationToken _token) {
            return (conditions.Check(_context) ? then : otherwise).RunSequence(_context, _token);
        }
    }
}
