using System;
using UnityEngine;
using HarryTools.Actions;
using HarryTools.Conditions;
using HarryTools.SerializeReferences;

namespace HarryTools.Interactions.Actions {
    [Serializable, TypeMenu("Interactions/Set Interaction Text")]
    public class SetInteractionTextAction : InstantAction {
        // ==================== VARIABLES ===================
        [SerializeField] private TargetComponent<Interactable> target = new(TargetSource.Self);
        [Tooltip("The first interaction of this type, active or not."), SerializeField] private InteractionType type;
        [SerializeReference, TypeSelector] private IInteractionText text;

        // ==================== CONSTRUCTORS ===================
        public SetInteractionTextAction() { }

        internal SetInteractionTextAction(TargetComponent<Interactable> _target, InteractionType _type, IInteractionText _text) {
            target = _target ?? new TargetComponent<Interactable>(TargetSource.Self);
            type = _type;
            text = _text;
        }

        // ==================== METHODS ===================
        protected override void Execute(LogicContext _context) {
            Interactable _interactable = target.Resolve(_context);
            if (_interactable != null && _interactable.TryGetInteractionAnyState(type, out Interaction _interaction)) _interaction.SetText(text);
        }
    }
}
