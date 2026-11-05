using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using HarryTools.Actions;
using HarryTools.Conditions;
using HarryTools.SerializeReferences;
using UnityEngine;

namespace HarryTools.Audio.Actions {
    [Serializable, TypeMenu("Audio/Play Conversation")]
    public class PlayConversationAction : IAction {
        // ==================== VARIABLES ===================
        [SerializeField] private ConversationAsset conversation;
        [Tooltip("Waits until the conversation ends. If the list is cancelled, the conversation stops."), SerializeField] private bool waitUntilFinished = true;

        // ==================== CONSTRUCTORS ===================
        public PlayConversationAction() { }

        internal PlayConversationAction(ConversationAsset _conversation, bool _waitUntilFinished) {
            conversation = _conversation;
            waitUntilFinished = _waitUntilFinished;
        }

        // ==================== INTERFACES ===================
        public async UniTask Run(LogicContext _context, CancellationToken _token) {
            VoiceChannel _channel = Audio.Voices;
            if (conversation == null || _channel == null) return;
            ConversationHandle _handle = _channel.Play(conversation);

            if (!waitUntilFinished) return;
            try {
                await _handle.WaitForEnd(_token);
            } catch (OperationCanceledException) {
                _handle.Stop(0.1f);
                throw;
            }
        }
    }
}
