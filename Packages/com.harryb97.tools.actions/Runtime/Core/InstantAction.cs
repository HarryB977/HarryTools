using System;
using System.Threading;

using HarryTools.Conditions;

using Cysharp.Threading.Tasks;

namespace HarryTools.Actions {
    /// <summary> Base for actions that finish in the same frame. Only 'Execute' needs to be implemented </summary>
    [Serializable]
    public abstract class InstantAction : IAction {
        // ==================== INTERFACES ====================
        public UniTask Run(LogicContext _context, CancellationToken _token) {
            Execute(_context);
            return UniTask.CompletedTask;
        }

        // ==================== METHODS ====================
        protected abstract void Execute(LogicContext _context);
    }
}
