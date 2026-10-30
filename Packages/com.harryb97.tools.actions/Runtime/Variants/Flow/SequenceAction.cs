using System;
using System.Threading;
using UnityEngine;

using HarryTools.Conditions;
using HarryTools.SerializeReferences;

using Cysharp.Threading.Tasks;

namespace HarryTools.Actions.Variants {
    /// <summary> Agrupa varias acciones en una sola. Útil como rama de 'Parallel' o para organizar listas largas </summary>
    [Serializable, TypeMenu("Flow/Sequence", -96)]
    public class SequenceAction : IAction {
        // ==================== VARIABLES ===================
        #region Private Variables
        [Tooltip("Solo para organizar el inspector"), SerializeField] private string label;
        [SerializeReference, TypeSelector] private IAction[] actions = Array.Empty<IAction>();
        #endregion

        // ==================== INTERFACES ====================
        public UniTask Run(LogicContext _context, CancellationToken _token) => actions.RunSequence(_context, _token);

        public override string ToString() => string.IsNullOrWhiteSpace(label) ? base.ToString() : label;
    }
}
