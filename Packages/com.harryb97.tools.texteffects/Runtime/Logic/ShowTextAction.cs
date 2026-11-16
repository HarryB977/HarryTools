using System;
using System.Threading;
using UnityEngine;

using HarryTools.Actions;
using HarryTools.Conditions;
using HarryTools.SerializeReferences;

using Cysharp.Threading.Tasks;

namespace HarryTools.TextEffects {
    /// <summary> Starts typing the text of an EffectText, optionally waiting until it finishes </summary>
    [Serializable, TypeMenu("Text Effects/Show Text")]
    public sealed class ShowTextAction : IAction {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private TargetComponent<EffectText> target = new TargetComponent<EffectText>();
        [Tooltip("Waits until the text has finished typing, including any wait for input until it is continued"), SerializeField] private bool waitUntilComplete;
        #endregion

        // ==================== CONSTRUCTORS ===================
        public ShowTextAction() { }

        public ShowTextAction(TargetComponent<EffectText> _target, bool _waitUntilComplete) {
            target = _target;
            waitUntilComplete = _waitUntilComplete;
        }

        // ==================== INTERFACES ===================
        public async UniTask Run(LogicContext _context, CancellationToken _token) {
            EffectText _text = target?.Resolve(_context);
            if (_text == null) return;

            if (!waitUntilComplete) {
                _text.Show();
                return;
            }

            TextActionWaiter _waiter = new TextActionWaiter();
            Action _finish = _waiter.Finish;
            _text.TypingCompleted += _finish;
            _text.Hidden += _finish;
            try {
                _text.Show();
                if (_waiter.Done) return;

                // The state is read after a frame so a Show deferred by the text has been applied by then
                while (true) {
                    await UniTask.Yield(PlayerLoopTiming.Update, _token);
                    if (_waiter.Done || _text == null || !_text.isActiveAndEnabled) return;
                    if (_text.State == EffectTextState.Complete || _text.State == EffectTextState.Hidden) return;
                }
            } finally {
                if (_text != null) {
                    _text.TypingCompleted -= _finish;
                    _text.Hidden -= _finish;
                }
            }
        }
    }
}
