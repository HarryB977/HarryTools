using UnityEngine;

namespace HarryTools.Audio {
    /// <summary> When a SoundPlayer plays its sound by itself. </summary>
    public enum SoundPlayerTrigger {
        OnEnable,
        OnStart,
        Manual
    }

    /// <summary>
    /// Plays a sound from the inspector, no code: when it is enabled, in Start, or when Play is called (from a UnityEvent, for example).
    /// </summary>
    [AddComponentMenu("HarryTools/Audio/Sound Player")]
    public sealed class SoundPlayer : MonoBehaviour {
        // ==================== VARIABLES ===================
        [Tooltip("Sound to play.")]
        public SoundAsset Sound;
        [Tooltip("When it plays by itself. Manual only plays when Play is called.")]
        public SoundPlayerTrigger PlayOn = SoundPlayerTrigger.OnEnable;
        [Tooltip("The sound follows this object. Off plays it at the position this object had when the sound started.")]
        public bool FollowSelf = true;
        [Tooltip("Stops the sound (with the FadeOut of the asset) when this component is disabled.")]
        public bool StopOnDisable = true;

        private AudioHandle handle;

        // ==================== UNITY ===================
        private void OnEnable() {
            if (PlayOn == SoundPlayerTrigger.OnEnable) Play();
        }

        private void Start() {
            if (PlayOn == SoundPlayerTrigger.OnStart && isActiveAndEnabled) Play();
        }

        private void OnDisable() {
            if (StopOnDisable) Stop();
        }

        // ==================== PUBLIC ===================
        /// <summary> Plays the sound. If it is already playing, it restarts. </summary>
        public void Play() {
            if (Sound == null) return;
            handle.Stop(0f);
            handle = FollowSelf ? Audio.Play(Sound, transform) : Audio.Play(Sound, transform.position);
        }

        /// <summary> Stops the sound with the FadeOut of the asset. </summary>
        public void Stop() {
            handle.Stop();
            handle = AudioHandle.Invalid;
        }
    }
}
