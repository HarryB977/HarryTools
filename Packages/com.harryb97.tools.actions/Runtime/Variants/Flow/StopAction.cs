using System;
using System.Threading;
using UnityEngine;

using HarryTools.Conditions;
using HarryTools.SerializeReferences;

using Cysharp.Threading.Tasks;

namespace HarryTools.Actions.Variants {
    /// <summary> Detiene la ejecución: no se ejecuta ninguna acción más de la lista (ni de las listas que la contienen) </summary>
    [Serializable, TypeMenu("Flow/Stop", -90)]
    public class StopAction : IAction {
        // ==================== VARIABLES ===================
        #region Private Variables
        [Tooltip("Solo se detiene si se cumplen (vacío = siempre)"), SerializeField] private ConditionList conditions = new();
        #endregion

        // ==================== INTERFACES ====================
        public UniTask Run(LogicContext _context, CancellationToken _token) {
            if (conditions.Check(_context)) throw new OperationCanceledException("Stop");
            return UniTask.CompletedTask;
        }
    }
}
