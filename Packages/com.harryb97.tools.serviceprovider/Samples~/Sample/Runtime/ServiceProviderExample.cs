using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

using HarryTools.ServiceLocator.Example.Dev;
using HarryTools.ServiceLocator.Example.Prod;

namespace HarryTools.ServiceLocator.Example {
    [DefaultExecutionOrder(-1)]
    public class ServiceProvider : MonoBehaviour {
        public enum Context { Development, Production }
        
        // ==================== VARIABLES ===================
        #region Private Variables
        private static IServiceScope Scope;

        [SerializeField] private Context context;
        #endregion
        
        // ==================== START ====================
        private void Awake() {
            // Se escoje un Scope de la manera deseada. Convenientemente, con un 'enum'
            Scope = context == Context.Development ? new DevScopeExample() : new ProdScopeExample();
        }
        
        // ==================== METHODS ====================
        public static void GetService<T>(out T _service) => Scope.GetService<T>(out _service);
        public static void GetService(Type _type, out object _service) => Scope.GetService(_type, out _service);

        public static void GetServices(MonoBehaviour _target) {
            Type _targetType = _target.GetType();
            FieldInfo[] _fields = _targetType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            // Debug.Log($"Target type: {_targetType}");

            for (int i = 0; i < _fields.Length; i++) {
                // Debug.Log($"Field: {_field.Name}, type: {_field.FieldType}");

                IEnumerable<Attribute> _attributes = _fields[i].GetCustomAttributes();
                
                foreach (Attribute _attribute in _attributes) {
                    if (_attribute is not ServiceAttribute) continue;

                    // Debug.Log($"Attribute: {_attribute.GetType()}");

                    Type _serviceType = _fields[i].FieldType;
                    GetService(_serviceType, out object _service);
                    _fields[i].SetValue(_target, _service);
                }
            }

            /*
            foreach (FieldInfo _field in _fields) {
                // Debug.Log($"Field: {_field.Name}, type: {_field.FieldType}");

                IEnumerable<Attribute> _attributes = _field.GetCustomAttributes();
                foreach (Attribute _attribute in _attributes) {
                    if (_attribute is not ServiceAttribute) continue;

                    // Debug.Log($"Attribute: {_attribute.GetType()}");

                    Type _serviceType = _field.FieldType;
                    ServiceProvider.GetService(_serviceType, out object _service);
                    _field.SetValue(_target, _service);
                }
            }
            */
        }
    }
}