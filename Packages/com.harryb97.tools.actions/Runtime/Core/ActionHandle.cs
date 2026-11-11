using System;
using System.Threading;
using UnityEngine;

using Cysharp.Threading.Tasks;

namespace HarryTools.Actions {
    /// <summary>
    /// Represents a running execution. It can be cancelled, awaited, or inspected to see how it ended.
    /// Replaces the static dictionary of active executions: each execution has its own control.
    /// </summary>
    public sealed class ActionHandle {
        public enum HandleStatus {
            Running,
            Completed,
            Cancelled,
            Faulted
        }

        // ==================== VARIABLES ===================
        #region Public Variables
        public HandleStatus Status { get; private set; } = HandleStatus.Running;
        public bool IsRunning => Status == HandleStatus.Running;

        /// <summary> Can be awaited any number of times. Never throws </summary>
        public UniTask Task { get; }

        /// <summary> Invoked when it finishes, whatever the reason </summary>
        public event Action<ActionHandle> Finished;
        #endregion

        #region Private Variables
        private CancellationTokenSource source;
        #endregion

        // ==================== START ====================
        /// <param name="_work"> Work to run, receives the linked token </param>
        /// <param name="_token"> External token: cancelling it also cancels this execution </param>
        public ActionHandle(Func<CancellationToken, UniTask> _work, CancellationToken _token = default) {
            source = CancellationTokenSource.CreateLinkedTokenSource(_token);
            Task = Execute(_work, source.Token).Preserve();
        }

        // ==================== METHODS ====================
        public void Cancel() {
            if (!IsRunning) return;
            source?.Cancel();
        }

        private async UniTask Execute(Func<CancellationToken, UniTask> _work, CancellationToken _token) {
            try {
                await _work(_token);
                Status = HandleStatus.Completed;
            } catch (OperationCanceledException) {
                Status = HandleStatus.Cancelled;
            } catch (Exception _exception) {
                Status = HandleStatus.Faulted;
                Debug.LogException(_exception);
            } finally {
                source?.Dispose();
                source = null;

                Finished?.Invoke(this);
            }
        }
    }
}
