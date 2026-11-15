using System;
using UnityEngine;
using HarryTools.Actions;
using HarryTools.Conditions;
using HarryTools.SerializeReferences;

namespace HarryTools.Interactions.Actions {
    [Serializable, TypeMenu("Interactions/Set Interaction Active")]
    public class SetInteractionActiveAction : InstantAction {
        // ==================== VARIABLES ===================
        [SerializeField] private TargetComponent<Interactable> target = new(TargetSource.Self);
        [Tooltip("The first interaction of this type, active or not."), SerializeField] private InteractionType type;
        [SerializeField] private bool active = true;

        // ==================== CONSTRUCTORS ===================
        public SetInteractionActiveAction() { }

        internal SetInteractionActiveAction(TargetComponent<Interactable> _target, InteractionType _type, bool _active) {
            target = _target ?? new TargetComponent<Interactable>(TargetSource.Self);
            type = _type;
            active = _active;
        }

        // ==================== METHODS ===================
        protected override void Execute(LogicContext _context) {
            Interactable _interactable = target.Resolve(_context);
            if (_interactable != null && _interactable.TryGetInteractionAnyState(type, out Interaction _interaction)) _interaction.SetActive(active);
        }
    }
}
