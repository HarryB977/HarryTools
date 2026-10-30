using System;
using System.Threading;
using UnityEngine;

using HarryTools.Conditions;
using HarryTools.SerializeReferences;

using Cysharp.Threading.Tasks;

namespace HarryTools.Actions.Variants {
    /// <summary> Espera hasta que se cumplan las condiciones. Sustituye a los 'Requisites' que esperaban </summary>
    [Serializable, TypeMenu("Flow/Wait Until", -98)]
    public class WaitUntilAction : IAction {
        public enum TimeoutBehaviour {
            [Tooltip("Continúa con las siguientes acciones")] Continue,
            [Tooltip("Cancela el resto de la lista")] Cancel
        }

        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private ConditionList conditions = new();
        [Space]
        [Tooltip("Segundos máximos de espera (0 = sin límite)"), SerializeField, Min(0)] private float timeout;
        [SerializeField] private TimeoutBehaviour onTimeout = TimeoutBehaviour.Continue;
        [SerializeField] private bool ignoreTimeScale;
        #endregion

        // ==================== INTERFACES ====================
        public async UniTask Run(LogicContext _context, CancellationToken _token) {
            float _elapsed = 0;

            while (!conditions.Check(_context)) {
                if (timeout > 0 && _elapsed >= timeout) {
                    if (onTimeout == TimeoutBehaviour.Cancel) throw new OperationCanceledException("'Wait Until' ha superado el tiempo máximo");
                    return;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, _token);
                _elapsed += ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
            }
        }
    }
}
