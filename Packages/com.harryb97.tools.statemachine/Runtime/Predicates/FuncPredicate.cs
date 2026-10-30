using System;

namespace HarryTools.StateMachineTool {
    public class FuncPredicate : IPredicate {
        // ==================== VARIABLES ===================
        #region Private Variables
        private readonly Func<bool> func;
        #endregion
        
        // ==================== START ====================
        public FuncPredicate(Func<bool> _func) {
            func = _func;
        }

        // ==================== INTERFACES ====================
        #region IPredicate
        bool IPredicate.Evaluate() => func.Invoke();
        #endregion
    }
}