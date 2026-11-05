using System.Collections.Generic;
using HarryTools.DomainReload;
using UnityEngine;

namespace HarryTools.Audio {
    /// <summary> What an emitter does with a line that arrives while it is speaking. </summary>
    public enum EmitterBusyPolicy {
        /// <summary> The line waits until the previous ones end (FIFO). </summary>
        Queue,
        /// <summary> The current line fades out in 0.1 s and the new one starts. </summary>
        Interrupt
    }

    /// <summary>
    /// A point voices come from: a character, a phone, a radio. Lines said through it follow its transform and get its Effect
    /// (else the DefaultEffect of the speaker), one line at a time. Lines played without emitter use the active emitter of their
    /// speaker: the last one enabled that is still active and enabled.
    /// </summary>
    [AddComponentMenu("HarryTools/Audio/Audio Emitter")]
    public sealed class AudioEmitter : MonoBehaviour {
        // ==================== VARIABLES ===================
        // Enable order: the last entry wins. Scanned from the end; there are few emitters, and Speaker may change at runtime.
        [ClearOnReload(AssignNewInstance = true)] private static List<AudioEmitter> registered = new List<AudioEmitter>();

        [Tooltip("Speaker this emitter talks for. Lines of this speaker played without emitter come from here while it is the active one.")]
        public SpeakerAsset Speaker;
        [Tooltip("Effect applied to everything this emitter says or plays. Empty uses the DefaultEffect of the speaker.")]
        public AudioEffectPreset Effect;
        [Tooltip("Queue: lines wait their turn. Interrupt: a new line cuts the current one.")]
        public EmitterBusyPolicy BusyPolicy = EmitterBusyPolicy.Queue;

        // ==================== PROPERTIES ===================
        /// <summary> True while one of its lines has started (queued lines do not count). </summary>
        public bool IsSpeaking {
            get {
                AudioRuntime _runtime = Audio.Current;
                return _runtime != null && !_runtime.IsDisposed && _runtime.VoiceChannel.IsSpeaking(this);
            }
        }

        // ==================== UNITY ===================
        private void OnEnable() {
            registered.Remove(this);
            registered.Add(this);
        }

        private void OnDisable() => registered.Remove(this);

        private void OnDestroy() => registered.Remove(this);

        // ==================== PUBLIC ===================
        /// <summary> Plays the sound from the emitter with its effect, following it. Sounds are not queued. </summary>
        public AudioHandle Play(SoundAsset _sound) {
            if (_sound == null) return AudioHandle.Invalid;
            AudioRuntime _runtime = Audio.Runtime;
            if (_runtime == null) return AudioHandle.Invalid;
            AudioEffectPreset _effect = Effect != null ? Effect : Speaker != null ? Speaker.DefaultEffect : null;
            return _runtime.Voices.Start(new PlaybackRequest(_sound, transform.position, transform, this, _effect, false, false));
        }

        /// <summary> Says the line through this emitter. Same as Audio.Voices.Play(_voice, this). </summary>
        public AudioHandle Say(VoiceAsset _voice) {
            VoiceChannel _channel = Audio.Voices;
            return _channel != null ? _channel.Play(_voice, this) : AudioHandle.Invalid;
        }

        /// <summary> The emitter of the speaker enabled last that is still active and enabled. </summary>
        public static bool TryGetActive(SpeakerAsset _speaker, out AudioEmitter _emitter) {
            if (_speaker != null) {
                for (int _i = registered.Count - 1; _i >= 0; _i--) {
                    AudioEmitter _candidate = registered[_i];
                    if (_candidate != null && _candidate.isActiveAndEnabled && ReferenceEquals(_candidate.Speaker, _speaker)) {
                        _emitter = _candidate;
                        return true;
                    }
                }
            }
            _emitter = null;
            return false;
        }
    }
}
