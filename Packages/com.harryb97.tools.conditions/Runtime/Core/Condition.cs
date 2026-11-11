using System;
using UnityEngine;

namespace HarryTools.Conditions {
    /// <summary> Optional base for conditions: adds an option to invert the result from the inspector </summary>
    [Serializable]
    public abstract class Condition : ICondition {
        // ==================== VARIABLES ===================
        #region Private Variables
        [Tooltip("Inverts the result of the condition"), SerializeField] private bool invert;
        #endregion

        // ==================== METHODS ====================
        public bool Check(LogicContext _context) => Evaluate(_context) != invert;

        protected abstract bool Evaluate(LogicContext _context);
    }
}
