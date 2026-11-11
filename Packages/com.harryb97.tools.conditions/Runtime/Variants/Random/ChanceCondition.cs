using System;
using UnityEngine;

using HarryTools.SerializeReferences;

using Random = UnityEngine.Random;

namespace HarryTools.Conditions.Variants {
    [Serializable, TypeMenu("Random/Chance")]
    public class ChanceCondition : Condition {
        // ==================== VARIABLES ===================
        #region Private Variables
        [Tooltip("Chance that it passes (0 = never, 1 = always)"), SerializeField, Range(0, 1)] private float probability = 0.5f;
        #endregion

        // ==================== METHODS ====================
        protected override bool Evaluate(LogicContext _context) => Random.value < probability;
    }
}
