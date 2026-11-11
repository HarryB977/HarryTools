using System;
using System.Threading;
using UnityEngine;

using HarryTools.Conditions;
using HarryTools.SerializeReferences;

using Cysharp.Threading.Tasks;

namespace HarryTools.Actions.Variants {
    /// <summary> Waits until the conditions are met. Replaces the 'Requisites' that used to wait </summary>
    [Serializable, TypeMenu("Flow/Wait Until", -98)]
    public class WaitUntilAction : IAction {
        public enum TimeoutBehaviour {
            [Tooltip("Continues with the next actions")] Continue,
            [Tooltip("Cancels the rest of the list")] Cancel
        }

        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private ConditionList conditions = new();
        [Space]
        [Tooltip("Maximum wait in seconds (0 = no limit)"), SerializeField, Min(0)] private float timeout;
        [SerializeField] private TimeoutBehaviour onTimeout = TimeoutBehaviour.Continue;
        [SerializeField] private bool ignoreTimeScale;
        #endregion

        // ==================== INTERFACES ====================
        public async UniTask Run(LogicContext _context, CancellationToken _token) {
            float _elapsed = 0;

            while (!conditions.Check(_context)) {
                if (timeout > 0 && _elapsed >= timeout) {
                    if (onTimeout == TimeoutBehaviour.Cancel) throw new OperationCanceledException("'Wait Until' exceeded the maximum time");
                    return;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, _token);
                _elapsed += ignoreTimeScale ? Time.unscaledDeltaTime : Time.deltaTime;
            }
        }
    }
}
