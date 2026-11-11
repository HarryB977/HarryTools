using System;
using System.Threading;
using UnityEngine;

using HarryTools.Conditions;
using HarryTools.SerializeReferences;

using Cysharp.Threading.Tasks;

namespace HarryTools.Actions.Variants {
    [Serializable, TypeMenu("Flow/Repeat", -94)]
    public class RepeatAction : IAction {
        // ==================== VARIABLES ===================
        #region Private Variables
        [Tooltip("Number of repetitions (0 = infinite, until cancelled)"), SerializeField, Min(0)] private int count = 2;
        [Tooltip("If met, repeating stops (checked before each iteration)"), SerializeField] private ConditionList stopWhen = new();
        [SerializeReference, TypeSelector] private IAction[] actions = Array.Empty<IAction>();
        #endregion

        // ==================== INTERFACES ====================
        public async UniTask Run(LogicContext _context, CancellationToken _token) {
            for (int i = 0; count == 0 || i < count; i++) {
                if (!stopWhen.IsEmpty && stopWhen.Check(_context)) return;

                await actions.RunSequence(_context, _token);

                // Avoids freezing the game if every action is instant and the loop is infinite
                if (count == 0) await UniTask.Yield(PlayerLoopTiming.Update, _token);
            }
        }
    }
}
