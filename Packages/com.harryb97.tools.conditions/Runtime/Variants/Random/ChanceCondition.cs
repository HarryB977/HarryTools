using System;
using UnityEngine;

using HarryTools.SerializeReferences;

using Random = UnityEngine.Random;

namespace HarryTools.Conditions.Variants {
    [Serializable, TypeMenu("Random/Chance")]
    public class ChanceCondition : Condition {
        // ==================== VARIABLES ===================
        #region Private Variables
        [Tooltip("Probabilidad de que se cumpla (0 = nunca, 1 = siempre)"), SerializeField, Range(0, 1)] private float probability = 0.5f;
        #endregion

        // ==================== METHODS ====================
        protected override bool Evaluate(LogicContext _context) => Random.value < probability;
    }
}
