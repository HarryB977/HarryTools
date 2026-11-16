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
        [Tooltip("Waits until the text has finished typing"), SerializeField] private bool waitUntilComplete;
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

            _text.Show();
            if (!waitUntilComplete || _text.State == EffectTextState.Complete) return;

            UniTaskCompletionSource _source = new UniTaskCompletionSource();
            Action _finish = () => _source.TrySetResult();
            _text.TypingCompleted += _finish;
            _text.Hidden += _finish;
            try {
                using (_token.Register(() => _source.TrySetCanceled(_token))) {
                    await _source.Task;
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
