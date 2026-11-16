using HarryTools.Actions;

namespace HarryTools.TextEffects {
    /// <summary> Resolves text event names to action lists </summary>
    public interface ITextEventProvider {
        bool TryGetEvent(string _name, out ActionList _actions, out bool _runWhenSkipped);
    }
}
