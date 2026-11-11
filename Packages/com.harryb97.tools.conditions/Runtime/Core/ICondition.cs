namespace HarryTools.Conditions {
    /// <summary>
    /// Synchronous check without side effects. To wait for something to happen, use the 'Wait Until' action.
    /// Implementations must be [Serializable] to show up in the [TypeSelector] dropdown.
    /// </summary>
    public interface ICondition {
        bool Check(LogicContext _context);
    }
}
