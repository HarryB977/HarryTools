using System;
using UnityEngine;

namespace HarryTools.SerializeReferences {
    /// <summary>
    /// Muestra en el inspector un desplegable con los tipos que heredan del tipo del campo.
    /// Debe usarse junto al atributo [SerializeReference].
    /// </summary>
    /// <example> [SerializeReference, TypeSelector] private IMyInterface[] values; </example>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class TypeSelectorAttribute : PropertyAttribute {
        // ==================== VARIABLES ===================
        #region Public Variables
        /// <summary> Usa el 'ToString()' de la instancia como etiqueta del campo </summary>
        public bool UseToStringAsLabel { get; set; }

        /// <summary> Si es falso, el desplegable no ofrecerá la opción de dejar el campo vacío </summary>
        public bool AllowNull { get; set; } = true;
        #endregion
    }
}
