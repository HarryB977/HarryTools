using System;
using UnityEngine;
using UnityEngine.Events;

using HarryTools.Conditions;
using HarryTools.SerializeReferences;

namespace HarryTools.Actions.Variants {
    /// <summary> Invokes a UnityEvent that receives the given GameObject (for example, the context 'Target') </summary>
    [Serializable, TypeMenu("Events/Invoke GameObject Event", -79)]
    public class InvokeGameObjectEventAction : InstantAction {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private TargetGameObject argument = new(TargetSource.Target);
        [SerializeField] private UnityEvent<GameObject> unityEvent = new();
        #endregion

        // ==================== METHODS ====================
        protected override void Execute(LogicContext _context) => unityEvent?.Invoke(argument.Resolve(_context));
    }
}
