using System;
using System.Threading;

using HarryTools.Conditions;

using Cysharp.Threading.Tasks;

namespace HarryTools.Actions {
    /// <summary> Base para acciones que terminan en el mismo frame. Solo hay que implementar 'Execute' </summary>
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
