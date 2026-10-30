using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace HarryTools.DomainReload.Editor {
    internal static class DomainReloadHandler {
        // ==================== START ====================
        [InitializeOnEnterPlayMode]
        private static void OnEnterPlayMode(EnterPlayModeOptions _options) {
            // Con recarga de dominio Unity ya reinicia los estáticos
            if (!_options.HasFlag(EnterPlayModeOptions.DisableDomainReload)) return;

            ClearFields();
            ExecuteMethods();
        }

        // ==================== METHODS ====================
        private static void ClearFields() {
            foreach (FieldInfo _field in TypeCache.GetFieldsWithAttribute<ClearOnReloadAttribute>()) {
                if (!IsValidMember(_field.DeclaringType, _field.IsStatic, _field.Name)) continue;

                if (_field.IsInitOnly || _field.IsLiteral) {
                    Debug.LogWarning($"[DomainReload] '{_field.DeclaringType}.{_field.Name}' es readonly o const y no se puede limpiar");
                    continue;
                }

                ClearOnReloadAttribute _attribute = _field.GetCustomAttribute<ClearOnReloadAttribute>();

                try {
                    _field.SetValue(null, GetClearValue(_field.FieldType, _attribute));

                } catch (Exception _exc) {
                    Debug.LogError($"[DomainReload] Fallo al limpiar '{_field.DeclaringType}.{_field.Name}': {_exc.Message}");
                }
            }
        }

        private static void ExecuteMethods() {
            foreach (MethodInfo _method in TypeCache.GetMethodsWithAttribute<ExecuteOnReloadAttribute>()) {
                if (!IsValidMember(_method.DeclaringType, _method.IsStatic, _method.Name)) continue;

                if (_method.GetParameters().Length != 0) {
                    Debug.LogWarning($"[DomainReload] '{_method.DeclaringType}.{_method.Name}' tiene parámetros y no se puede ejecutar");
                    continue;
                }

                try {
                    _method.Invoke(null, null);

                } catch (Exception _exc) {
                    Debug.LogError($"[DomainReload] Fallo al ejecutar '{_method.DeclaringType}.{_method.Name}': {(_exc.InnerException ?? _exc).Message}");
                }
            }
        }

        // =================================================

        private static bool IsValidMember(Type _declaringType, bool _isStatic, string _name) {
            // Un genérico abierto no tiene una instancia concreta de sus estáticos
            if (_declaringType == null || _declaringType.ContainsGenericParameters) return false;

            if (!_isStatic) {
                Debug.LogWarning($"[DomainReload] '{_declaringType}.{_name}' no es estático y se ignora");
                return false;
            }

            return true;
        }

        private static object GetClearValue(Type _fieldType, ClearOnReloadAttribute _attribute) {
            if (_attribute.AssignNewInstance) return Activator.CreateInstance(_fieldType);
            if (_attribute.Value != null) return _attribute.Value;

            return _fieldType.IsValueType ? Activator.CreateInstance(_fieldType) : null;
        }
    }
}
