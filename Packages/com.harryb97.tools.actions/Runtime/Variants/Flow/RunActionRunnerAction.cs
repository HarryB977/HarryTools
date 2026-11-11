using System;
using System.Threading;
using UnityEngine;

using HarryTools.Conditions;
using HarryTools.SerializeReferences;

using Cysharp.Threading.Tasks;

namespace HarryTools.Actions.Variants {
    /// <summary> Runs the actions of another 'ActionRunner' (replaces 'Play Sequence') </summary>
    [Serializable, TypeMenu("Flow/Run Action Runner", -92)]
    public class RunActionRunnerAction : IAction {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private TargetComponent<ActionRunner> runner = new();
        [Tooltip("Waits for it to finish before continuing"), SerializeField] private bool wait = true;
        [Tooltip("Passes the current context 'Target' to the runner"), SerializeField] private bool forwardTarget;
        #endregion

        // ==================== INTERFACES ====================
        public async UniTask Run(LogicContext _context, CancellationToken _token) {
            ActionRunner _runner = runner?.Resolve(_context);
            if (_runner == null) return;

            ActionHandle _handle = _runner.RunAndGetHandle(forwardTarget ? _context.Target : null);
            if (!wait || _handle == null) return;

            // If this list is cancelled, the runner's execution is cancelled too
            using (_token.Register(_handle.Cancel)) {
                await _handle.Task;
            }

            _token.ThrowIfCancellationRequested();
        }
    }
}
