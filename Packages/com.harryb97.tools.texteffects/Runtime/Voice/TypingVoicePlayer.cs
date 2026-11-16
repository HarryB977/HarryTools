#if HARRYTOOLS_TEXTEFFECTS_AUDIO
using HarryTools.Audio;
using UnityEngine;

namespace HarryTools.TextEffects {
    /// <summary> Plays a typing voice through the audio package </summary>
    internal static class TypingVoicePlayer {
        // ==================== METHODS ====================
        #region Internal Methods
        /// <summary> Plays the voice sound. In world space it follows '_at'; otherwise it has no position. Does nothing without a sound </summary>
        internal static void Play(TypingVoice _voice, Transform _at, bool _worldSpace) {
            if (_voice == null) return;
            SoundRef _sound = _voice.Sound;
            if (!_sound.IsValid) return;

            AudioHandle _handle = _worldSpace && _at != null ? HarryTools.Audio.Audio.Play(_sound, _at) : HarryTools.Audio.Audio.Play(_sound);
            Vector2 _pitch = _voice.PitchRange;
            _handle.WithPitch(_pitch.x, _pitch.y);
        }
        #endregion
    }
}
#endif
