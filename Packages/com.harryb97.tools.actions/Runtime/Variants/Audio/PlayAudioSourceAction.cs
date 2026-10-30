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
        [Tooltip("Clip a reproducir (vacío = el que ya tenga asignado)"), SerializeField] private AudioClip clip;
        [Tooltip("Reproduce con 'PlayOneShot', permitiendo solapar sonidos"), SerializeField] private bool oneShot;
        [Tooltip("Espera a que termine el sonido. Si se cancela la lista, se detiene"), SerializeField] private bool waitUntilFinished;
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
                // 'isPlaying' es falso durante un frame tras llamar a 'Play' en algunas plataformas
                await UniTask.Yield(PlayerLoopTiming.Update, _token);
                while (_source != null && (_source.isPlaying || AudioListener.pause)) await UniTask.Yield(PlayerLoopTiming.Update, _token);
            } catch (OperationCanceledException) {
                if (_source != null) _source.Stop();
                throw;
            }
        }
    }
}
