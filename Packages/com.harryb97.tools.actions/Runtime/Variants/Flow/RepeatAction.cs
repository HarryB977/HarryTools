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
        [Tooltip("Número de repeticiones (0 = infinito, hasta que se cancele)"), SerializeField, Min(0)] private int count = 2;
        [Tooltip("Si se cumplen, se deja de repetir (se comprueba antes de cada vuelta)"), SerializeField] private ConditionList stopWhen = new();
        [SerializeReference, TypeSelector] private IAction[] actions = Array.Empty<IAction>();
        #endregion

        // ==================== INTERFACES ====================
        public async UniTask Run(LogicContext _context, CancellationToken _token) {
            for (int i = 0; count == 0 || i < count; i++) {
                if (!stopWhen.IsEmpty && stopWhen.Check(_context)) return;

                await actions.RunSequence(_context, _token);

                // Evita congelar el juego si todas las acciones son instantáneas y el bucle es infinito
                if (count == 0) await UniTask.Yield(PlayerLoopTiming.Update, _token);
            }
        }
    }
}
