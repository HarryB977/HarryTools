using System;
using UnityEngine;

using HarryTools.SerializeReferences;

namespace HarryTools.Conditions.Variants {
    [Serializable, TypeMenu("GameObject/Is Active")]
    public class GameObjectIsActive : Condition {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private TargetGameObject target = new();
        [Tooltip("Also requires the parents to be active"), SerializeField] private bool inHierarchy = true;
        #endregion

        // ==================== METHODS ====================
        protected override bool Evaluate(LogicContext _context) {
            GameObject _gameObject = target.Resolve(_context);
            if (_gameObject == null) return false;

            return inHierarchy ? _gameObject.activeInHierarchy : _gameObject.activeSelf;
        }
    }
}
