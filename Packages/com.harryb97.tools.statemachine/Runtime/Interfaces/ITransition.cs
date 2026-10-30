namespace HarryTools.StateMachineTool {
    public interface ITransition {
        IState To { get; }
        IPredicate Condition { get; }
    }
}