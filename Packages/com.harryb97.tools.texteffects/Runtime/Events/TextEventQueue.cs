using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using HarryTools.Actions;
using HarryTools.Conditions;
using Logger = HarryTools.LoggerTool.Logger;

namespace HarryTools.TextEffects {
    /// <summary> Runs action lists one after another, each waiting for the previous one to finish </summary>
    public sealed class TextEventQueue {
        // ==================== VARIABLES ===================
        #region Private Variables
        private readonly struct Item {
            public readonly ActionList Actions;
            public readonly LogicContext Context;

            public Item(ActionList _actions, LogicContext _context) {
                Actions = _actions;
                Context = _context;
            }
        }

        private readonly Queue<Item> pending = new Queue<Item>();
        private CancellationTokenSource cts;
        #endregion

        #region Public Variables
        public bool IsRunning { get; private set; }
        public int PendingCount => pending.Count;
        #endregion

        // ==================== METHODS ====================
        #region Public Methods
        public void Enqueue(ActionList _actions, LogicContext _context) {
            if (_actions == null) return;
            pending.Enqueue(new Item(_actions, _context));
            if (IsRunning) return;

            IsRunning = true;
            cts = new CancellationTokenSource();
            Loop(cts).Forget();
        }

        /// <summary> Cancels the running list and discards the pending ones. The queue can be used again afterwards </summary>
        public void Cancel() {
            pending.Clear();
            IsRunning = false;
            if (cts == null) return;
            CancellationTokenSource _old = cts;
            cts = null;
            _old.Cancel();
            _old.Dispose();
        }
        #endregion

        #region Loop
        private async UniTaskVoid Loop(CancellationTokenSource _source) {
            CancellationToken _token = _source.Token;
            while (!_token.IsCancellationRequested && pending.Count > 0) {
                Item _item = pending.Dequeue();
                try {
                    await _item.Actions.RunAsync(_item.Context, _token);
                } catch (OperationCanceledException) {
                    break;
                } catch (Exception _exception) {
                    Logger.LogError("TextEffects", "Text event actions failed: " + _exception);
                }
            }

            // A Cancel (and possibly a new Enqueue) may have replaced this loop's source already
            if (!ReferenceEquals(cts, _source)) return;
            cts = null;
            IsRunning = false;
            _source.Dispose();
        }
        #endregion
    }
}
