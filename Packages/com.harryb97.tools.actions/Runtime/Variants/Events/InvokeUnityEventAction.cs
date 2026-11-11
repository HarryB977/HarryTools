using System;
using UnityEngine;
using UnityEngine.Events;

using HarryTools.Conditions;
using HarryTools.SerializeReferences;

namespace HarryTools.Actions.Variants {
    /// <summary>
    /// Invokes a UnityEvent as one more step of the list, respecting order, conditions and cancellation.
    /// Replaces the standalone 'UnityEvent' that each process group used to have.
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
