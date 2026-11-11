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
        /// Runs the actions in order, waiting for each one to finish.
        /// Empty (null) entries are ignored. If an action throws, the exception is logged to the console and the next one runs.
        /// </summary>
        /// <exception cref="OperationCanceledException"> If the token is cancelled </exception>
        public static async UniTask RunSequence(this IReadOnlyList<IAction> _actions, LogicContext _context, CancellationToken _token) {
            if (_actions == null) return;

            for (int i = 0; i < _actions.Count; i++) {
                _token.ThrowIfCancellationRequested();

                if (_actions[i] == null) continue;
                await _actions[i].RunSafe(_context, _token);
            }
        }

        /// <summary> Runs an action, logging to the console any exception that is not a cancellation </summary>
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

        /// <summary> Starts the execution without waiting and returns an 'ActionHandle' to control it </summary>
        public static ActionHandle Start(this IReadOnlyList<IAction> _actions, LogicContext _context, CancellationToken _token = default) {
            return new ActionHandle(_linkedToken => _actions.RunSequence(_context, _linkedToken), _token);
        }
    }
}
