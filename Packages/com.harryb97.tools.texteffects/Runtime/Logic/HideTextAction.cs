using System;
using System.Threading;
using UnityEngine;

using HarryTools.Actions;
using HarryTools.Conditions;
using HarryTools.SerializeReferences;

using Cysharp.Threading.Tasks;

namespace HarryTools.TextEffects {
    /// <summary> Plays the hide of an EffectText, optionally waiting until it is hidden </summary>
    [Serializable, TypeMenu("Text Effects/Hide Text")]
    public sealed class HideTextAction : IAction {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private TargetComponent<EffectText> target = new TargetComponent<EffectText>();
        [Tooltip("Waits until the text is hidden"), SerializeField] private bool waitUntilHidden;
        #endregion

        // ==================== CONSTRUCTORS ===================
        public HideTextAction() { }

        public HideTextAction(TargetComponent<EffectText> _target, bool _waitUntilHidden) {
            target = _target;
            waitUntilHidden = _waitUntilHidden;
        }

        // ==================== INTERFACES ===================
        public async UniTask Run(LogicContext _context, CancellationToken _token) {
            EffectText _text = target?.Resolve(_context);
            if (_text == null) return;

            if (!waitUntilHidden) {
                _text.Hide();
                return;
            }
            if (_text.State == EffectTextState.Hidden) return;

            TextActionWaiter _waiter = new TextActionWaiter();
            Action _finish = _waiter.Finish;
            _text.Hidden += _finish;
            try {
                _text.Hide();
                // A Hide deferred by the text has been applied by the next frame
                if (!_waiter.Done && _text.State != EffectTextState.Hiding) await UniTask.Yield(PlayerLoopTiming.Update, _token);

                // Leaving Hiding without Hidden means a Show interrupted the hide
                while (!_waiter.Done && _text != null && _text.isActiveAndEnabled && _text.State == EffectTextState.Hiding) {
                    await UniTask.Yield(PlayerLoopTiming.Update, _token);
                }
            } finally {
                if (_text != null) _text.Hidden -= _finish;
            }
        }
    }
}
