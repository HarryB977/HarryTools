using System;
using UnityEngine;

using HarryTools.Conditions;
using HarryTools.SerializeReferences;

namespace HarryTools.Actions.Variants {
    [Serializable, TypeMenu("GameObject/Set Behaviour Enabled")]
    public class SetBehaviourEnabledAction : InstantAction {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private TargetComponent<Behaviour>[] behaviours = Array.Empty<TargetComponent<Behaviour>>();
        [SerializeField] private ToggleMode mode = ToggleMode.Enable;
        #endregion

        // ==================== METHODS ====================
        protected override void Execute(LogicContext _context) {
            if (behaviours == null) return;

            foreach (TargetComponent<Behaviour> _target in behaviours) {
                Behaviour _behaviour = _target?.Resolve(_context);
                if (_behaviour == null) continue;

                _behaviour.enabled = mode switch {
                    ToggleMode.Enable => true,
                    ToggleMode.Disable => false,
                    _ => !_behaviour.enabled
                };
            }
        }
    }
}
