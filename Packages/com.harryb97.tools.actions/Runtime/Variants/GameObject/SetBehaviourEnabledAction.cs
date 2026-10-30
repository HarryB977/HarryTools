using System;
using UnityEngine;

using HarryTools.Conditions;
using HarryTools.SerializeReferences;

namespace HarryTools.Actions.Variants {
    [Serializable, TypeMenu("GameObject/Set Behaviour Enabled")]
    public class SetBehaviourEnabledAction : InstantAction {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private Behaviour[] behaviours = Array.Empty<Behaviour>();
        [SerializeField] private ToggleMode mode = ToggleMode.Enable;
        #endregion

        // ==================== METHODS ====================
        protected override void Execute(LogicContext _context) {
            foreach (Behaviour _behaviour in behaviours) {
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
