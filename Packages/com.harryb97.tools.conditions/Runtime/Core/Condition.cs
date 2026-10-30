using System;
using UnityEngine;

namespace HarryTools.Conditions {
    /// <summary> Base opcional para condiciones: añade la opción de invertir el resultado desde el inspector </summary>
    [Serializable]
    public abstract class Condition : ICondition {
        // ==================== VARIABLES ===================
        #region Private Variables
        [Tooltip("Invierte el resultado de la condición"), SerializeField] private bool invert;
        #endregion

        // ==================== METHODS ====================
        public bool Check(LogicContext _context) => Evaluate(_context) != invert;

        protected abstract bool Evaluate(LogicContext _context);
    }
}
