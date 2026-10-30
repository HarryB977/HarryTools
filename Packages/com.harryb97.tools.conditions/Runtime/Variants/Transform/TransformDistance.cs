using System;
using UnityEngine;

using HarryTools.SerializeReferences;

namespace HarryTools.Conditions.Variants {
    [Serializable, TypeMenu("Transform/Distance")]
    public class TransformDistance : Condition {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private TargetGameObject from = new(TargetSource.Self);
        [SerializeField] private TargetGameObject to = new(TargetSource.Target);
        [Space]
        [SerializeField] private Comparison comparison = Comparison.LessOrEqual;
        [SerializeField, Min(0)] private float distance = 1f;
        #endregion

        // ==================== METHODS ====================
        protected override bool Evaluate(LogicContext _context) {
            GameObject _from = from.Resolve(_context);
            GameObject _to = to.Resolve(_context);
            if (_from == null || _to == null) return false;

            return comparison.Evaluate(Vector3.Distance(_from.transform.position, _to.transform.position), distance);
        }
    }
}
