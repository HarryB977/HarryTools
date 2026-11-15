using System;
using UnityEngine;
using HarryTools.Actions;
using HarryTools.Conditions;
using HarryTools.SerializeReferences;

namespace HarryTools.Interactions.Actions {
    [Serializable, TypeMenu("Interactions/Set Interactable Active")]
    public class SetInteractableActiveAction : InstantAction {
        // ==================== VARIABLES ===================
        [SerializeField] private TargetComponent<Interactable> target = new(TargetSource.Self);
        [SerializeField] private bool active = true;

        // ==================== CONSTRUCTORS ===================
        public SetInteractableActiveAction() { }

        internal SetInteractableActiveAction(TargetComponent<Interactable> _target, bool _active) {
            target = _target ?? new TargetComponent<Interactable>(TargetSource.Self);
            active = _active;
        }

        // ==================== METHODS ===================
        protected override void Execute(LogicContext _context) {
            Interactable _interactable = target.Resolve(_context);
            if (_interactable != null) _interactable.SetActive(active);
        }
    }
}
