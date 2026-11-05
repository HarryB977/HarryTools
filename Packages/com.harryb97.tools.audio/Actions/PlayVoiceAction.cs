using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using HarryTools.Actions;
using HarryTools.Conditions;
using HarryTools.SerializeReferences;
using UnityEngine;

namespace HarryTools.Audio.Actions {
    [Serializable, TypeMenu("Audio/Play Voice")]
    public class PlayVoiceAction : IAction {
        // ==================== VARIABLES ===================
        [SerializeField] private VoiceAsset voice;
        [Tooltip("Emitter that says the line. Empty uses the active emitter of the speaker."), SerializeField] private TargetComponent<AudioEmitter> emitter = new();
        [Tooltip("Waits until the line ends. If the list is cancelled, the line stops."), SerializeField] private bool waitUntilFinished = true;

        // ==================== CONSTRUCTORS ===================
        public PlayVoiceAction() { }

        internal PlayVoiceAction(VoiceAsset _voice, TargetComponent<AudioEmitter> _emitter, bool _waitUntilFinished) {
            voice = _voice;
            emitter = _emitter ?? new TargetComponent<AudioEmitter>();
            waitUntilFinished = _waitUntilFinished;
        }

        // ==================== INTERFACES ===================
        public async UniTask Run(LogicContext _context, CancellationToken _token) {
            VoiceChannel _channel = Audio.Voices;
            if (voice == null || _channel == null) return;
            AudioHandle _handle = _channel.Play(voice, emitter.Resolve(_context));

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
