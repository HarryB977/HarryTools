using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

using HarryTools.Conditions;
using HarryTools.SerializeReferences;

using Cysharp.Threading.Tasks;

namespace HarryTools.Actions.Variants {
    /// <summary> Runs all actions at once. For a branch with several steps, use 'Sequence' </summary>
    [Serializable, TypeMenu("Flow/Parallel", -95)]
    public class ParallelAction : IAction {
        public enum WaitMode {
            [Tooltip("Waits for all of them to finish")] All,
            [Tooltip("Waits for the first one and cancels the rest")] Any
        }

        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private WaitMode waitFor = WaitMode.All;
        [SerializeReference, TypeSelector] private IAction[] actions = Array.Empty<IAction>();
        #endregion

        // ==================== INTERFACES ====================
        public async UniTask Run(LogicContext _context, CancellationToken _token) {
            if (actions == null || actions.Length == 0) return;

            using CancellationTokenSource _source = CancellationTokenSource.CreateLinkedTokenSource(_token);
            List<UniTask> _tasks = new(actions.Length);

            foreach (IAction _action in actions) {
                if (_action != null) _tasks.Add(_action.RunSafe(_context, _source.Token));
            }

            if (_tasks.Count == 0) return;

            try {
                if (waitFor == WaitMode.All) await UniTask.WhenAll(_tasks);
                else await UniTask.WhenAny(_tasks.ToArray());
            } finally {
                // With 'Any', branches still running are cancelled. With 'All', only if it was cancelled from outside
                _source.Cancel();
            }

            _token.ThrowIfCancellationRequested();
        }
    }
}
