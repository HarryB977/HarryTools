using System;
using UnityEngine;

using HarryTools.SerializeReferences;

namespace HarryTools.Conditions.Variants {
    [Serializable, TypeMenu("GameObject/Compare Tag")]
    public class GameObjectCompareTag : Condition {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private TargetGameObject target = new(TargetSource.Target);
        [SerializeField] private string tag = "Player";
        #endregion

        // ==================== METHODS ====================
        protected override bool Evaluate(LogicContext _context) {
            GameObject _gameObject = target.Resolve(_context);
            return _gameObject != null && _gameObject.CompareTag(tag);
        }
    }
}
