using System.Threading;

using HarryTools.Conditions;

using Cysharp.Threading.Tasks;

namespace HarryTools.Actions {
    /// <summary>
    /// Piece of logic that can be run from the inspector. It can be instant or last several frames.
    /// Implementations must be [Serializable] to show up in the [TypeSelector] dropdown.
    /// If the action waits, it must honour the 'CancellationToken' (pass it to UniTask or check it every frame).
    /// </summary>
    public interface IAction {
        UniTask Run(LogicContext _context, CancellationToken _token);
    }
}
