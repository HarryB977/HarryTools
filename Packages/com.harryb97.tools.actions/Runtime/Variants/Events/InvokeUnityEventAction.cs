using System;
using UnityEngine;
using UnityEngine.Events;

using HarryTools.Conditions;
using HarryTools.SerializeReferences;

namespace HarryTools.Actions.Variants {
    /// <summary>
    /// Invoca un UnityEvent como un paso más de la lista, respetando el orden, las condiciones y la cancelación.
    /// Sustituye al 'UnityEvent' suelto que tenía cada grupo de procesos.
    /// </summary>
    [Serializable, TypeMenu("Events/Invoke Unity Event", -80)]
    public class InvokeUnityEventAction : InstantAction {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private UnityEvent unityEvent = new();
        #endregion

        // ==================== METHODS ====================
        protected override void Execute(LogicContext _context) => unityEvent?.Invoke();
    }
}
