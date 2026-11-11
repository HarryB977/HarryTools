using System;
using System.Threading;
using UnityEngine;

using HarryTools.Conditions;
using HarryTools.SerializeReferences;

using Cysharp.Threading.Tasks;

namespace HarryTools.Actions.Variants {
    [Serializable, TypeMenu("Audio/Play Audio Source")]
    public class PlayAudioSourceAction : IAction {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private TargetComponent<AudioSource> audioSource = new();
        [Tooltip("Clip to play (empty = the one already assigned)"), SerializeField] private AudioClip clip;
        [Tooltip("Plays with 'PlayOneShot', allowing sounds to overlap"), SerializeField] private bool oneShot;
        [Tooltip("Waits for the sound to finish. If the list is cancelled, it stops"), SerializeField] private bool waitUntilFinished;
        #endregion

        // ==================== INTERFACES ====================
        public async UniTask Run(LogicContext _context, CancellationToken _token) {
            AudioSource _source = audioSource.Resolve(_context);
            if (_source == null) return;

            if (oneShot) {
                AudioClip _clip = clip != null ? clip : _source.clip;
                if (_clip == null) return;

                _source.PlayOneShot(_clip);
                if (waitUntilFinished) await UniTask.Delay(TimeSpan.FromSeconds(_clip.length / Mathf.Max(0.01f, Mathf.Abs(_source.pitch))), DelayType.Realtime, PlayerLoopTiming.Update, _token);

                return;
            }

            if (clip != null) _source.clip = clip;
            _source.Play();

            if (!waitUntilFinished) return;

            try {
                // 'isPlaying' is false for one frame after calling 'Play' on some platforms
                await UniTask.Yield(PlayerLoopTiming.Update, _token);
                while (_source != null && (_source.isPlaying || AudioListener.pause)) await UniTask.Yield(PlayerLoopTiming.Update, _token);
            } catch (OperationCanceledException) {
                if (_source != null) _source.Stop();
                throw;
            }
        }
    }
}
