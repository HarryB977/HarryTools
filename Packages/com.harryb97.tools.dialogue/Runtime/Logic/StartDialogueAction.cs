using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using HarryTools.Actions;
using HarryTools.Conditions;
using HarryTools.SerializeReferences;
using UnityEngine;

namespace HarryTools.Dialogue {
    /// <summary> Starts a dialogue, optionally waiting for it to end </summary>
    [Serializable, TypeMenu("Dialogue/Start Dialogue")]
    public sealed class StartDialogueAction : IAction {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private DialogueAsset dialogue;
        [Tooltip("Main dialogues can offer choices and only one runs at a time"), SerializeField] private bool isMain = true;
        [Tooltip("The action finishes when the dialogue ends. Cancelling the action stops the wait, not the dialogue"), SerializeField] private bool waitUntilEnd;
        #endregion

        // ==================== START ====================
        public StartDialogueAction() { }

        public StartDialogueAction(DialogueAsset _dialogue, bool _isMain = true, bool _waitUntilEnd = false) {
            dialogue = _dialogue;
            isMain = _isMain;
            waitUntilEnd = _waitUntilEnd;
        }

        // ==================== METHODS ====================
        public async UniTask Run(LogicContext _context, CancellationToken _token) {
            DialogueStartOptions _options = new DialogueStartOptions {
                IsMain = isMain,
                Origin = _context.Target != null ? _context.Target : _context.Self
            };
            DialogueRun _run = global::HarryTools.Dialogue.Dialogue.Start(dialogue, _options);
            if (_run == null || !waitUntilEnd || _run.State == RunState.Ended) return;

            UniTaskCompletionSource _ended = new UniTaskCompletionSource();
            void OnEnded(DialogueRun _endedRun, EndReason _reason) => _ended.TrySetResult();
            _run.Ended += OnEnded;
            try {
                await _ended.Task.AttachExternalCancellation(_token);
            } finally {
                _run.Ended -= OnEnded;
            }
        }
    }
}
