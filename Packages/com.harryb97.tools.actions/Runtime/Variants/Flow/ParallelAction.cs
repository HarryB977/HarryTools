using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

using HarryTools.Conditions;
using HarryTools.SerializeReferences;

using Cysharp.Threading.Tasks;

namespace HarryTools.Actions.Variants {
    /// <summary> Ejecuta todas las acciones a la vez. Para que una rama tenga varios pasos, usa 'Sequence' </summary>
    [Serializable, TypeMenu("Flow/Parallel", -95)]
    public class ParallelAction : IAction {
        public enum WaitMode {
            [Tooltip("Espera a que terminen todas")] All,
            [Tooltip("Espera a la primera y cancela el resto")] Any
        }

        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private WaitMode waitFor = WaitMode.All;
        [SerializeReference, TypeSelector] private IAction[] actions = Array.Empty<IAction>();
        #endregion

        // ==================== INTERFACES ====================
        public async UniTask Run(LogicContext _context, CancellationToken _token) {
            if (actions == null || actions.Length == 0) return;

            using CancellationTokenSource _source = CancellationTokenSource.CreateLinkedTokenSource(_token);
            List<UniTask> _tasks = new(actions.Length);

            foreach (IAction _action in actions) {
                if (_action != null) _tasks.Add(_action.RunSafe(_context, _source.Token));
            }

            if (_tasks.Count == 0) return;

            try {
                if (waitFor == WaitMode.All) await UniTask.WhenAll(_tasks);
                else await UniTask.WhenAny(_tasks.ToArray());
            } finally {
                // Con 'Any', las ramas que siguen vivas se cancelan. Con 'All', solo si se canceló desde fuera
                _source.Cancel();
            }

            _token.ThrowIfCancellationRequested();
        }
    }
}
