namespace HarryTools.StateMachineTool {
    public abstract class BaseState : IState {
        // ==================== START ====================
        public BaseState() { }
        
        // ==================== METHODS ====================
        public virtual void OnEnter() { }
        
        public virtual void Ticks() { }

        public virtual void FixedTicks() { }
        public virtual void OnExit() { }
    }
}