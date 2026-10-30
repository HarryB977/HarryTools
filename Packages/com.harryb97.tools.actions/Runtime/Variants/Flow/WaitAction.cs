using System;
using System.Threading;
using UnityEngine;

using HarryTools.Conditions;
using HarryTools.SerializeReferences;

using Cysharp.Threading.Tasks;

namespace HarryTools.Actions.Variants {
    [Serializable, TypeMenu("Flow/Wait", -100)]
    public class WaitAction : IAction {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField, Min(0)] private float seconds = 1f;
        [Tooltip("Ignora 'Time.timeScale' (sigue contando con el juego pausado)"), SerializeField] private bool ignoreTimeScale;
        #endregion

        // ==================== INTERFACES ====================
        public UniTask Run(LogicContext _context, CancellationToken _token) {
            if (seconds <= 0) return UniTask.CompletedTask;

            DelayType _delayType = ignoreTimeScale ? DelayType.UnscaledDeltaTime : DelayType.DeltaTime;
            return UniTask.Delay(TimeSpan.FromSeconds(seconds), _delayType, PlayerLoopTiming.Update, _token);
        }
    }
}
