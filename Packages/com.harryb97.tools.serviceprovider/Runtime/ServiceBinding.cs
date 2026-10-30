using System;
using System.Reflection;
using UnityEngine;

namespace HarryTools.ServiceLocator {
    /// <summary> Asigna 'target' al campo marcado con [ServiceBindable] de un servicio mientras este componente exista </summary>
    [DisallowMultipleComponent]
    public class ServiceBinding : MonoBehaviour {
        // ==================== VARIABLES ===================
        #region Serialized Variables
        [SerializeField] private string serviceType;
        [SerializeField] private string fieldName;
        [SerializeField] private UnityEngine.Object target;
        #endregion

        #region Private Variables
        private const BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        #endregion

        // ==================== START ====================
        private void Awake() => ServiceBinder.Register(this);
        private void OnDestroy() => ServiceBinder.Unregister(this);

        // ==================== METHODS ====================
        internal void Bind(IServiceScope _scope) {
            if (!TryResolve(_scope, out object _service, out FieldInfo _field)) return;

            _field.SetValue(_service, target);
            if (_service is IServiceBindingCallbacks _callbacks) _callbacks.OnBind(fieldName);
        }

        internal void Unbind(IServiceScope _scope) {
            if (!TryResolve(_scope, out object _service, out FieldInfo _field)) return;

            // Solo se limpia si nadie ha reemplazado la referencia entretanto
            if (!ReferenceEquals(_field.GetValue(_service), target)) return;

            _field.SetValue(_service, null);
            if (_service is IServiceBindingCallbacks _callbacks) _callbacks.OnUnbind(fieldName);
        }

        private bool TryResolve(IServiceScope _scope, out object _service, out FieldInfo _field) {
            _service = null;
            _field = null;

            Type _type = string.IsNullOrEmpty(serviceType) ? null : Type.GetType(serviceType);
            if (_type == null) {
                Debug.LogError($"[ServiceBinding] '{name}': no se encuentra el tipo de servicio '{serviceType}'", this);
                return false;
            }

            _field = _type.GetField(fieldName ?? string.Empty, FieldFlags);
            if (_field == null || !_field.IsDefined(typeof(ServiceBindableAttribute))) {
                Debug.LogError($"[ServiceBinding] '{name}': '{_type.Name}.{fieldName}' no existe o no tiene [ServiceBindable]", this);
                return false;
            }

            if (target != null && !_field.FieldType.IsInstanceOfType(target)) {
                Debug.LogError($"[ServiceBinding] '{name}': '{target.GetType().Name}' no es asignable a '{_type.Name}.{fieldName}' ({_field.FieldType.Name})", this);
                return false;
            }

            if (!_scope.GetServiceByImplementation(_type, out _service)) {
                Debug.LogError($"[ServiceBinding] '{name}': no hay ningún servicio registrado de tipo '{_type.Name}'", this);
                return false;
            }

            return true;
        }
    }
}
