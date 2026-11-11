using System;
using System.Threading;
using UnityEngine;

using HarryTools.Conditions;
using HarryTools.SerializeReferences;

using Cysharp.Threading.Tasks;

namespace HarryTools.Actions {
    /// <summary>
    /// List of actions with preconditions, ready to use as a field in any component.
    /// If the conditions are not met, no action runs.
    /// </summary>
    /// <example>
    /// [SerializeField] private ActionList onOpen;
    /// ...
    /// onOpen.Run(this);                                   // Fire and forget, cancelled when the component is destroyed
    /// await onOpen.RunAsync(LogicContext.From(this), destroyCancellationToken);
    /// </example>
    [Serializable]
    public class ActionList {
        // ==================== VARIABLES ===================
        #region Properties
        public ConditionList Conditions => conditions;
        public IAction[] Actions => actions;
        public bool IsEmpty => actions == null || Array.TrueForAll(actions, _action => _action == null);
        #endregion

        #region Private Variables
        [SerializeField] private ConditionList conditions = new();
        [SerializeReference, TypeSelector] private IAction[] actions = Array.Empty<IAction>();
        #endregion

        // ==================== START ====================
        public ActionList() { }

        public ActionList(params IAction[] _actions) {
            actions = _actions;
        }

        public ActionList(ConditionList _conditions, params IAction[] _actions) {
            conditions = _conditions;
            actions = _actions;
        }

        // ==================== METHODS ====================
        /// <summary> Runs the list and waits for it to finish </summary>
        /// <returns> False if the conditions were not met </returns>
        /// <exception cref="OperationCanceledException"> If the token is cancelled </exception>
        public async UniTask<bool> RunAsync(LogicContext _context, CancellationToken _token = default) {
            if (conditions != null && !conditions.Check(_context)) return false;

            await actions.RunSequence(_context, _token);
            return true;
        }

        /// <summary> Runs the list without waiting and returns an 'ActionHandle' to control it </summary>
        public ActionHandle Run(LogicContext _context, CancellationToken _token = default) {
            return new ActionHandle(_linkedToken => RunAsync(_context, _linkedToken), _token);
        }

        /// <summary> Runs the list without waiting, with the component as 'Self'. Cancelled when the component is destroyed </summary>
        public ActionHandle Run(MonoBehaviour _owner, GameObject _target = null) {
            return Run(LogicContext.From(_owner, _target), _owner != null ? _owner.destroyCancellationToken : default);
        }
    }
}
