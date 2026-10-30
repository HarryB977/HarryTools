using System;
using System.Threading;
using UnityEngine;

using HarryTools.Conditions;
using HarryTools.SerializeReferences;

using Cysharp.Threading.Tasks;

namespace HarryTools.Actions {
    /// <summary>
    /// Lista de acciones con condiciones previas, lista para usar como campo en cualquier componente.
    /// Si las condiciones no se cumplen, no se ejecuta ninguna acción.
    /// </summary>
    /// <example>
    /// [SerializeField] private ActionList onOpen;
    /// ...
    /// onOpen.Run(this);                                   // Sin esperar, se cancela al destruir el componente
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
        /// <summary> Ejecuta la lista y espera a que termine </summary>
        /// <returns> Falso si las condiciones no se cumplieron </returns>
        /// <exception cref="OperationCanceledException"> Si se cancela el token </exception>
        public async UniTask<bool> RunAsync(LogicContext _context, CancellationToken _token = default) {
            if (conditions != null && !conditions.Check(_context)) return false;

            await actions.RunSequence(_context, _token);
            return true;
        }

        /// <summary> Ejecuta la lista sin esperar y devuelve un 'ActionHandle' para controlarla </summary>
        public ActionHandle Run(LogicContext _context, CancellationToken _token = default) {
            return new ActionHandle(_linkedToken => RunAsync(_context, _linkedToken), _token);
        }

        /// <summary> Ejecuta la lista sin esperar, con el componente como 'Self'. Se cancela al destruir el componente </summary>
        public ActionHandle Run(MonoBehaviour _owner, GameObject _target = null) {
            return Run(LogicContext.From(_owner, _target), _owner != null ? _owner.destroyCancellationToken : default);
        }
    }
}
