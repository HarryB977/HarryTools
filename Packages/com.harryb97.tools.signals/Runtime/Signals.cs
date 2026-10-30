using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace HarryTools.Signals {
    public class Signal : IReadOnlySignal {
        #region Builder
        public class Builder {
            private readonly Signal signal = new();

            public Builder() { }
            public Builder(string _id) : this() => signal.ID = _id;

            public Builder WithListener(Action _listener) {
                signal.OnDispatch += _listener;
                return this;
            }

            public Signal Build() {
                SignalManager.Register(signal);
                return signal;
            }
        }
        #endregion

        // ==================== VARIABLES ===================
        #region Public Variables
        public string ID { get; private set; }
        #endregion

        #region Private Variables
        private event Action OnDispatch;
        #endregion

        // ==================== METHODS ====================
        public void Dispatch() {
            OnDispatch?.Invoke();
        }
        
        // =================================================
        
        public static Builder New(string _id) => new(_id);
        public static Signal Get(string _id) => SignalManager.Get(_id);
        public static IReadOnlySignal GetReadOnly(string _id) => SignalManager.Get(_id);
        public static Task<Signal> GetAsync(string _id) => SignalManager.GetAsync(_id);
        public static async Task<IReadOnlySignal> GetReadOnlyAsync(string _id) => await SignalManager.GetAsync(_id);

        // ==================== INTERFACES ====================
        void IReadOnlySignal.AddListener(Action _listener) {
            OnDispatch += _listener;
        }

        void IReadOnlySignal.RemoveListener(Action _listener) {
            OnDispatch -= _listener;
        }
    }
    
    public class Signal<T> : IReadOnlySignal<T> {
        #region Builder
        public class Builder {
            private readonly Signal<T> signal = new();

            public Builder() { }
            public Builder(string _id) : this() => signal.ID = _id;

            public Builder WithValue(T _value) {
                signal.m_value = _value;
                return this;
            }

            public Builder WithListener(Action<T> _listener) {
                signal.OnDispatch += _listener;
                return this;
            }

            public Builder DispatchOnSameValue(bool _state) {
                signal.dispatchOnSameValue = _state;
                return this;
            }

            public Signal<T> Build() {
                SignalManager.Register(signal);
                return signal;
            }
        }
        #endregion

        // ==================== VARIABLES ===================
        #region Public Variables
        public string ID { get; private set; }
        public T Value {
            get => m_value;
            set {
                if (!dispatchOnSameValue && EqualityComparer<T>.Default.Equals(m_value, value)) return;

                m_value = value;
                OnDispatch?.Invoke(m_value);
            }
        }

        #endregion

        #region Private Variables
        private T m_value;
        private event Action<T> OnDispatch;

        private bool dispatchOnSameValue = true;
        #endregion

        // ==================== METHODS ====================
        public static Builder New(string _id) => new(_id);
        public static Signal<T> Get(string _id) => SignalManager.Get<T>(_id);
        public static IReadOnlySignal<T> GetReadOnly(string _id) => SignalManager.Get<T>(_id);
        public static Task<Signal<T>> GetAsync(string _id) => SignalManager.GetAsync<T>(_id);
        public static async Task<IReadOnlySignal<T>> GetReadOnlyAsync(string _id) => await SignalManager.GetAsync<T>(_id);

        // ==================== INTERFACES ====================
        void IReadOnlySignal<T>.AddListener(Action<T> _listener) {
            OnDispatch += _listener;
        }

        void IReadOnlySignal<T>.RemoveListener(Action<T> _listener) {
            OnDispatch -= _listener;
        }
    }
}