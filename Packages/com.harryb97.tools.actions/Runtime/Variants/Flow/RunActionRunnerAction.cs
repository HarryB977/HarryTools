using System;
using System.Threading;
using UnityEngine;

using HarryTools.Conditions;
using HarryTools.SerializeReferences;

using Cysharp.Threading.Tasks;

namespace HarryTools.Actions.Variants {
    /// <summary> Ejecuta las acciones de otro 'ActionRunner' (sustituye a 'Play Sequence') </summary>
    [Serializable, TypeMenu("Flow/Run Action Runner", -92)]
    public class RunActionRunnerAction : IAction {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private ActionRunner runner;
        [Tooltip("Espera a que termine antes de continuar"), SerializeField] private bool wait = true;
        [Tooltip("Pasa el 'Target' del contexto actual al runner"), SerializeField] private bool forwardTarget;
        #endregion

        // ==================== INTERFACES ====================
        public async UniTask Run(LogicContext _context, CancellationToken _token) {
            if (runner == null) return;

            ActionHandle _handle = runner.RunAndGetHandle(forwardTarget ? _context.Target : null);
            if (!wait || _handle == null) return;

            // Si se cancela esta lista, también se cancela la ejecución del runner
            using (_token.Register(_handle.Cancel)) {
                await _handle.Task;
            }

            _token.ThrowIfCancellationRequested();
        }
    }
}
