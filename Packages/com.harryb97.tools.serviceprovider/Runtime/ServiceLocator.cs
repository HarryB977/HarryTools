using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace HarryTools.ServiceLocator {
    public class ServiceLocator : IServiceLocator {
        // ==================== VARIABLES ===================
        #region Private Variables
        private readonly ConcurrentDictionary<Type, object> services = new();
        private readonly List<IServiceDispose> disposables = new();
        #endregion

        // ==================== METHODS ====================
        public void Register<T>(T _service) => Register(typeof(T), _service);
        public void Register(Type _type, object _service) {
            if (_service == null) throw new ArgumentNullException(nameof(_service));
            if (!_type.IsAssignableFrom(_service.GetType())) throw new ArgumentException($"Este servicio no es asignable para el tipo: {_type}");
            if (!services.TryAdd(_type, _service)) throw new ArgumentException($"El servicio de tipo {_type} ya se encuentra registrado");

            if (_service is IServiceDispose _disposable && !disposables.Contains(_disposable)) disposables.Add(_disposable);
        }

        public bool Unregister<T>(T _service) => Unregister(typeof(T), _service);
        public bool Unregister(Type _type, object _service) {
            if (_type == null) throw new ArgumentNullException(nameof(_type));
            if (!services.TryRemove(_type, out object _removed)) return false;

            if (_removed is IServiceDispose _disposable && !services.Values.Contains(_removed)) disposables.Remove(_disposable);
            return true;
        }
        
        public T Get<T>() => (T)Get(typeof(T));
        public object Get(Type _type) {
            if (_type == null) throw new ArgumentNullException(nameof(_type));
            if (!services.TryGetValue(_type, out object _value)) throw new KeyNotFoundException($"Service of type {_type} is not registered");

            return _value;
        }

        public bool TryGetByImplementation(Type _implementation, out object _service) {
            if (_implementation == null) throw new ArgumentNullException(nameof(_implementation));

            foreach (object _value in services.Values) {
                if (!_implementation.IsInstanceOfType(_value)) continue;

                _service = _value;
                return true;
            }

            _service = null;
            return false;
        }

        public bool IsRegistered<T>() => IsRegistered(typeof(T));
        public bool IsRegistered(Type _type) {
            if(_type == null) throw new ArgumentNullException(nameof(_type));
            return services.ContainsKey(_type);
        }

        public void DisposeAll() {
            foreach (IServiceDispose _disposable in disposables) {
                try {
                    _disposable.Dispose();

                } catch (Exception _exc) {
                    UnityEngine.Debug.LogError($"Fallo al deshacerse del servicio {_disposable.GetType()}: {_exc.Message}");
                }
            }

            disposables.Clear();
            services.Clear();
        }
    }
}