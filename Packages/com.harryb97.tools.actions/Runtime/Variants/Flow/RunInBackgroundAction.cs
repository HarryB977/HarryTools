using System;
using System.Threading;
using UnityEngine;

using HarryTools.Conditions;
using HarryTools.SerializeReferences;

using Cysharp.Threading.Tasks;

namespace HarryTools.Actions.Variants {
    /// <summary> Lanza las acciones sin esperar a que terminen, y la lista continúa inmediatamente </summary>
    [Serializable, TypeMenu("Flow/Run In Background", -93)]
    public class RunInBackgroundAction : IAction {
        // ==================== VARIABLES ===================
        #region Private Variables
        [Tooltip("Si se cancela la lista principal, también se cancelan estas acciones"), SerializeField] private bool cancelWithParent = true;
        [SerializeReference, TypeSelector] private IAction[] actions = Array.Empty<IAction>();
        #endregion

        // ==================== INTERFACES ====================
        public UniTask Run(LogicContext _context, CancellationToken _token) {
            CancellationToken _backgroundToken = cancelWithParent ? _token : _context.Self != null ? _context.Self.GetCancellationTokenOnDestroy() : default;

            actions.Start(_context, _backgroundToken);
            return UniTask.CompletedTask;
        }
    }
}
