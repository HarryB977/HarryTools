using System;
using UnityEngine;

using HarryTools.Conditions;
using HarryTools.SerializeReferences;

using Object = UnityEngine.Object;

namespace HarryTools.Actions.Variants {
    [Serializable, TypeMenu("GameObject/Destroy")]
    public class DestroyAction : InstantAction {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private TargetGameObject target = new();
        [SerializeField, Min(0)] private float delay;
        #endregion

        // ==================== METHODS ====================
        protected override void Execute(LogicContext _context) {
            GameObject _gameObject = target.Resolve(_context);
            if (_gameObject != null) Object.Destroy(_gameObject, delay);
        }
    }
}
