using System;
using System.Threading;
using UnityEngine;

using HarryTools.Conditions;
using HarryTools.SerializeReferences;

using Cysharp.Threading.Tasks;

namespace HarryTools.Actions.Variants {
    /// <summary> Starts the actions without waiting for them to finish, and the list continues immediately </summary>
    [Serializable, TypeMenu("Flow/Run In Background", -93)]
    public class RunInBackgroundAction : IAction {
        // ==================== VARIABLES ===================
        #region Private Variables
        [Tooltip("If the main list is cancelled, these actions are cancelled too"), SerializeField] private bool cancelWithParent = true;
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
