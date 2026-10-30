namespace HarryTools.StateMachineTool {
    public interface IState {
        void OnEnter();
        
        void Ticks();
        void FixedTicks();
        
        void OnExit();
    }
}