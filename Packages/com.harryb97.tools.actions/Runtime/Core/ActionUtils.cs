using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

using HarryTools.Conditions;

using Cysharp.Threading.Tasks;

namespace HarryTools.Actions {
    public static class ActionUtils {
        // ==================== METHODS ====================
        /// <summary>
        /// Ejecuta las acciones en orden, esperando a que termine cada una.
        /// Las vacías (null) se ignoran. Si una acción lanza una excepción, se muestra en consola y se continúa con la siguiente.
        /// </summary>
        /// <exception cref="OperationCanceledException"> Si se cancela el token </exception>
        public static async UniTask RunSequence(this IReadOnlyList<IAction> _actions, LogicContext _context, CancellationToken _token) {
            if (_actions == null) return;

            for (int i = 0; i < _actions.Count; i++) {
                _token.ThrowIfCancellationRequested();

                if (_actions[i] == null) continue;
                await _actions[i].RunSafe(_context, _token);
            }
        }

        /// <summary> Ejecuta una acción mostrando en consola cualquier excepción que no sea de cancelación </summary>
        public static async UniTask RunSafe(this IAction _action, LogicContext _context, CancellationToken _token) {
            if (_action == null) return;

            try {
                await _action.Run(_context, _token);
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception _exception) {
                Debug.LogException(_exception, _context.Self);
            }
        }

        /// <summary> Inicia la ejecución sin esperar y devuelve un 'ActionHandle' para controlarla </summary>
        public static ActionHandle Start(this IReadOnlyList<IAction> _actions, LogicContext _context, CancellationToken _token = default) {
            return new ActionHandle(_linkedToken => _actions.RunSequence(_context, _linkedToken), _token);
        }
    }
}
