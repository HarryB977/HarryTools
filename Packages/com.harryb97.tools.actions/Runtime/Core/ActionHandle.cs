using System;
using System.Threading;
using UnityEngine;

using Cysharp.Threading.Tasks;

namespace HarryTools.Actions {
    /// <summary>
    /// Representa una ejecución en curso. Permite cancelarla, esperarla o saber cómo terminó.
    /// Sustituye al diccionario estático de ejecuciones activas: cada ejecución tiene su propio control.
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

        /// <summary> Se puede esperar tantas veces como se quiera. Nunca lanza excepción </summary>
        public UniTask Task { get; }

        /// <summary> Se invoca al terminar, sea cual sea el motivo </summary>
        public event Action<ActionHandle> Finished;
        #endregion

        #region Private Variables
        private CancellationTokenSource source;
        #endregion

        // ==================== START ====================
        /// <param name="_work"> Trabajo a ejecutar, recibe el token enlazado </param>
        /// <param name="_token"> Token externo: cancelarlo también cancela esta ejecución </param>
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
