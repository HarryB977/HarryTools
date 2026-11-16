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

            UniTaskCompletionSource _source = new UniTaskCompletionSource();
            Action _finish = () => _source.TrySetResult();
            _text.Hidden += _finish;
            try {
                _text.Hide();
                if (_text.State == EffectTextState.Hidden) return;

                using (_token.Register(() => _source.TrySetCanceled(_token))) {
                    await _source.Task;
                }
            } finally {
                if (_text != null) _text.Hidden -= _finish;
            }
        }
    }
}
