namespace HarryTools.StateMachineTool {
    public class Transition : ITransition {
        // ==================== VARIABLES ===================
        #region Public Variables
        public IState To { get; private set; }
        public IPredicate Condition { get; private set; }
        #endregion
        
        // ==================== START ====================
        public Transition(IState _to, IPredicate _condition) {
            To = _to;
            Condition = _condition;
        }
    }
}