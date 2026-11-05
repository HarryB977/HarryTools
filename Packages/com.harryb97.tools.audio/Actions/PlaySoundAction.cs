using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using HarryTools.Actions;
using HarryTools.Conditions;
using HarryTools.SerializeReferences;
using UnityEngine;

namespace HarryTools.Audio.Actions {
    [Serializable, TypeMenu("Audio/Play Sound")]
    public class PlaySoundAction : IAction {
        // ==================== VARIABLES ===================
        [SerializeField] private SoundAsset sound;
        [Tooltip("Where the sound comes from. Empty plays it at the listener."), SerializeField] private TargetComponent<Transform> at = new();
        [Tooltip("The sound follows the target. Off plays it where the target was."), SerializeField] private bool follow;
        [Tooltip("Waits until the sound ends. If the list is cancelled, the sound stops."), SerializeField] private bool waitUntilFinished;

        // ==================== CONSTRUCTORS ===================
        public PlaySoundAction() { }

        internal PlaySoundAction(SoundAsset _sound, TargetComponent<Transform> _at, bool _follow, bool _waitUntilFinished) {
            sound = _sound;
            at = _at ?? new TargetComponent<Transform>();
            follow = _follow;
            waitUntilFinished = _waitUntilFinished;
        }

        // ==================== INTERFACES ===================
        public async UniTask Run(LogicContext _context, CancellationToken _token) {
            if (sound == null) return;
            Transform _target = at.Resolve(_context);
            AudioHandle _handle;
            if (_target == null) _handle = Audio.Play(sound);
            else if (follow) _handle = Audio.Play(sound, _target);
            else _handle = Audio.Play(sound, _target.position);

            if (!waitUntilFinished) return;
            try {
                await _handle.WaitForEnd(_token);
            } catch (OperationCanceledException) {
                _handle.Stop();
                throw;
            }
        }
    }
}
