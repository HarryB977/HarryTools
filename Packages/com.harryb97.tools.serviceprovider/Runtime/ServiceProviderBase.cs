using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace HarryTools.ServiceLocator {
    public abstract class ServiceProviderBase<TInstance> : MonoBehaviour where TInstance : MonoBehaviour {
        // ==================== VARIABLES ===================
        #region Delegates
        public delegate void OnFinishConfigureDelegate();
        public OnFinishConfigureDelegate OnFinishConfigure;
        #endregion
        
        #region Public Variables
        // Singleton
        public static TInstance Instance {
            get {
                if (instance == null) {
                    instance = (TInstance)FindFirstObjectByType(typeof(TInstance));

                    if (instance == null) {
                        string _goName = typeof(TInstance).ToString();
                        GameObject _go = GameObject.Find(_goName);

                        if (_go == null) _go = new() {
                            name = _goName
                        };

                        instance = _go.AddComponent<TInstance>();
                    }
                }

                return instance;
            }
        }

        // References
        public bool IsInitialized { get; private set; } = false;
        #endregion

        #region Private Variables
        // Singleton
		private static TInstance instance = null;

        // Scope
        protected abstract IServiceScope Scope { get; }
        #endregion

        // ==================== START ===================
        private void OnDisable() {
            ServiceBinder.ClearScope(Scope);
            Scope.DisposeServices();
        }

        // ==================== METHODS ====================
        protected abstract void SetScope();
        public void Initialize() {
            SetScope();
            
            IsInitialized = true;
            ServiceBinder.SetScope(Scope);
            OnFinishConfigure?.Invoke();
        }
        
        public void GetService<TService>(out TService _service) => Scope.GetService(out _service);
        public void GetService(Type _type, out object _service) => Scope.GetService(_type, out _service);

        public void GetServices(MonoBehaviour _target) {
            if (!IsInitialized) return;
            
            Type _targetType = _target.GetType();
            FieldInfo[] _fields = _targetType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            foreach (FieldInfo _field in _fields) {
                IEnumerable<Attribute> _attributes = _field.GetCustomAttributes();
                
                foreach (Attribute _attribute in _attributes) {
                    if (_attribute is not ServiceAttribute) continue;

                    Type _serviceType = _field.FieldType;
                    GetService(_serviceType, out object _service);
                    _field.SetValue(_target, _service);
                }
            }
        }
    }
}