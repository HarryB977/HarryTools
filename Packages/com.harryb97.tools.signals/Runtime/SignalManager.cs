using System;
using System.Threading.Tasks;
using System.Collections.Generic;

using HarryTools.Disposable;
using HarryTools.LoggerTool;

namespace HarryTools.Signals {
    internal static class SignalManager {
        // ==================== VARIABLES ===================
        #region Private Variables
        private static readonly Dictionary<string, object> Signals = new();
        private static readonly Dictionary<string, TaskCompletionSource<object>> PendingRequests = new();
        #endregion

        // ==================== START ====================
        [StaticDispose]
        private static void Dispose() {
            Signals.Clear();
            PendingRequests.Clear();
        }

        // ==================== METHODS ====================
        #region Register
        public static void Register(Signal _signal) {
            if (string.IsNullOrEmpty(_signal.ID)) {
                throw new ArgumentException("El ID de la señal no puede ser nulo o estar vacío.");
            }

            if (Signals.ContainsKey(_signal.ID)) {
                // throw new InvalidOperationException($"Ya existe una señal registrada con el ID '{_signal.ID}'");
                
                Logger.LogWarning("SignalManager", $"Ya existe una señal registrada con el ID '{_signal.ID}'");
                return;
            }

            Signals[_signal.ID] = _signal;

            if (PendingRequests.TryGetValue(_signal.ID, out TaskCompletionSource<object> _tcs)) {
                _tcs.SetResult(_signal);
                PendingRequests.Remove(_signal.ID);
            }
        }
        
        public static void Register<T>(Signal<T> _signal) {
            if (string.IsNullOrEmpty(_signal.ID)) {
                throw new ArgumentException("El ID de la señal no puede ser nulo o estar vacío.");
            }

            if (Signals.ContainsKey(_signal.ID)) {
                // throw new InvalidOperationException($"Ya existe una señal registrada con el ID '{_signal.ID}'");
                
                Logger.LogWarning("SignalManager", $"Ya existe una señal registrada con el ID '{_signal.ID}'");
                return;
            }

            Signals[_signal.ID] = _signal;

            if (PendingRequests.TryGetValue(_signal.ID, out TaskCompletionSource<object> _tcs)) {
                _tcs.SetResult(_signal);
                PendingRequests.Remove(_signal.ID);
            }
        }
        #endregion

        // =================================================

        #region Get
        public static Signal Get(string _id) {
            if (Signals.TryGetValue(_id, out object _signalObject)) {
                if (_signalObject is Signal _signal) return _signal;
                throw new InvalidCastException($"La señal con ID '{_id}' no es del tipo '{typeof(Signal)}', sino del tipo '{_signalObject.GetType()}'");
            }

            throw new KeyNotFoundException($"No se ha encontrado ninguna señal con ID '{_id}'");
        }
        
        public static Signal<T> Get<T>(string _id) {
            if (Signals.TryGetValue(_id, out object _signalObject)) {
                if (_signalObject is Signal<T> _signal) return _signal;
                throw new InvalidCastException($"La señal con ID '{_id}' no es del tipo '{typeof(Signal<T>)}', sino del tipo '{_signalObject.GetType()}'");
            }

            throw new KeyNotFoundException($"No se ha encontrado ninguna señal con ID '{_id}'");
        }

        // =================================================

        public static Task<Signal> GetAsync(string _id) {
            if (Signals.TryGetValue(_id, out object _signalObject)) {
                if (_signalObject is Signal _signal) return Task.FromResult(_signal);
                return Task.FromException<Signal>(new InvalidCastException($"La señal con ID '{_id}' no es del tipo '{typeof(Signal)}'"));
            }

            if (!PendingRequests.TryGetValue(_id, out TaskCompletionSource<object> _tcs)) {
                _tcs = new();
                PendingRequests[_id] = _tcs;
            }

            Task<Signal> _typedTask = _tcs.Task.ContinueWith(task => {
                if (task.Result is Signal _result) return _result;
                throw new InvalidCastException($"La señal con ID '{_id}' no es del tipo '{typeof(Signal)}'");

            }, TaskContinuationOptions.ExecuteSynchronously);

            return _typedTask;
        }

        public static Task<Signal<T>> GetAsync<T>(string _id) {
            if (Signals.TryGetValue(_id, out object _signalObject)) {
                if (_signalObject is Signal<T> _signal) return Task.FromResult(_signal);
                return Task.FromException<Signal<T>>(new InvalidCastException($"La señal con ID '{_id}' no es del tipo '{typeof(Signal<T>)}'"));
            }

            if (!PendingRequests.TryGetValue(_id, out TaskCompletionSource<object> _tcs)) {
                _tcs = new();
                PendingRequests[_id] = _tcs;
            }

            Task<Signal<T>> _typedTask = _tcs.Task.ContinueWith(task => {
                if (task.Result is Signal<T> _result) return _result;
                throw new InvalidCastException($"La señal con ID '{_id}' no es del tipo '{typeof(Signal<T>)}'");

            }, TaskContinuationOptions.ExecuteSynchronously);

            return _typedTask;
        }
        #endregion
    }
}