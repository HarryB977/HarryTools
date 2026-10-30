using System.Collections.Generic;
using UnityEngine;

namespace HarryTools.ServiceLocator {
    /// <summary> Registro de 'ServiceBinding'. Guarda los que llegan antes de que el 'ServiceProvider' se inicialice y los aplica al recibir el Scope </summary>
    public static class ServiceBinder {
        // ==================== VARIABLES ===================
        #region Private Variables
        private static IServiceScope scope;
        private static readonly List<ServiceBinding> bindings = new();
        #endregion

        // ==================== START ====================
        // Reinicia el estado al entrar en el Modo Play sin recarga de dominio
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() {
            scope = null;
            bindings.Clear();
        }

        // ==================== METHODS ====================
        internal static void SetScope(IServiceScope _scope) {
            scope = _scope;
            // Copia: un OnBind puede crear objetos que registren nuevos bindings
            foreach (ServiceBinding _binding in bindings.ToArray()) _binding.Bind(scope);
        }

        internal static void ClearScope(IServiceScope _scope) {
            if (scope == _scope) scope = null;
        }

        internal static void Register(ServiceBinding _binding) {
            if (bindings.Contains(_binding)) return;

            bindings.Add(_binding);
            if (scope != null) _binding.Bind(scope);
        }

        internal static void Unregister(ServiceBinding _binding) {
            if (!bindings.Remove(_binding)) return;
            if (scope != null) _binding.Unbind(scope);
        }
    }
}
