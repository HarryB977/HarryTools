using System;
using UnityEngine;

using HarryTools.Conditions;
using HarryTools.SerializeReferences;

namespace HarryTools.Actions.Variants {
    public enum ToggleMode {
        Enable,
        Disable,
        Toggle
    }

    [Serializable, TypeMenu("GameObject/Set Active")]
    public class SetActiveAction : InstantAction {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private TargetGameObject target = new();
        [SerializeField] private ToggleMode mode = ToggleMode.Enable;
        #endregion

        // ==================== METHODS ====================
        protected override void Execute(LogicContext _context) {
            GameObject _gameObject = target.Resolve(_context);
            if (_gameObject == null) return;

            _gameObject.SetActive(mode switch {
                ToggleMode.Enable => true,
                ToggleMode.Disable => false,
                _ => !_gameObject.activeSelf
            });
        }
    }
}
