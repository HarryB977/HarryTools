using System;

namespace HarryTools.DomainReload {
    /// <summary>
    /// Restaura un campo estático al entrar en el Modo Play cuando la recarga de dominio está desactivada.
    /// Sin parámetros, asigna el valor por defecto del tipo.
    /// </summary>
    /// <example> [ClearOnReload] private static Action OnChanged; </example>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class ClearOnReloadAttribute : Attribute {
        // ==================== VARIABLES ===================
        #region Public Variables
        /// <summary> Valor que se asigna al campo. Se ignora si 'AssignNewInstance' es verdadero </summary>
        public object Value { get; }

        /// <summary> Asigna una instancia nueva del tipo del campo (necesita constructor sin parámetros) </summary>
        public bool AssignNewInstance { get; set; }
        #endregion

        // ==================== START ====================
        public ClearOnReloadAttribute() { }

        public ClearOnReloadAttribute(object _value) {
            Value = _value;
        }
    }
}
