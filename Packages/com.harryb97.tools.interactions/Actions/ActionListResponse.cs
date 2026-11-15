using System;
using UnityEngine;
using HarryTools.Actions;
using HarryTools.SerializeReferences;
using Object = UnityEngine.Object;

namespace HarryTools.Interactions.Actions {
    /// <summary> Ejecuta una lista de acciones con 'Self' = el Interactable y 'Target' = el GameObject del Interactor </summary>
    [Serializable, TypeMenu("Action List")]
    public class ActionListResponse : IInteractionResponse {
        // ==================== VARIABLES ===================
        [SerializeField] private ActionList actions = new();

        private ActionHandle handle;

        // ==================== CONSTRUCTORS ===================
        public ActionListResponse() { }

        internal ActionListResponse(ActionList _actions) {
            actions = _actions ?? new ActionList();
        }

        // ==================== INTERFACES ===================
        public bool IsRunning => handle != null && handle.IsRunning;

        public void Respond(Interactable _source, IInteractor _interactor) {
            if (_source == null || actions == null) return;

            GameObject _target = _interactor is Object _unityObject && _unityObject == null ? null : _interactor?.GameObject;
            handle = actions.Run(_source, _target);
        }
    }
}
