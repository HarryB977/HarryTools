using UnityEngine;
using UnityEngine.Audio;

namespace HarryTools.Audio {
    /// <summary>
    /// Writes an AudioEffectPreset onto the filters and the output of a voice. The low-pass and high-pass cutoffs of the preset
    /// are also stored on the voice, so the acoustic modules can combine them with their own (lowest low-pass, highest high-pass).
    /// The voice resets its filters when it goes back to the pool.
    /// </summary>
    internal static class EffectApplier {
        // ==================== PUBLIC ===================
        /// <summary> Applies the preset (null clears it) with the default output used when the preset has none. Does nothing without mixer. </summary>
        public static void Apply(AudioVoice _voice, AudioEffectPreset _preset, AudioMixerGroup _defaultOutput) {
            if (!AudioPlatform.UsesMixer || _voice == null) return;
            bool _hasPreset = _preset != null;

            bool _lowPass = _hasPreset && _preset.LowPass;
            AudioLowPassFilter _lowFilter = _voice.LowPass;
            if (_lowFilter != null) {
                if (_lowPass) {
                    _lowFilter.cutoffFrequency = _preset.LowPassCutoff;
                    _lowFilter.lowpassResonanceQ = _preset.LowPassResonance;
                }
                _lowFilter.enabled = _lowPass;
            }

            bool _highPass = _hasPreset && _preset.HighPass;
            AudioHighPassFilter _highFilter = _voice.HighPass;
            if (_highFilter != null) {
                if (_highPass) _highFilter.cutoffFrequency = _preset.HighPassCutoff;
                _highFilter.enabled = _highPass;
            }

            bool _distortion = _hasPreset && _preset.Distortion;
            AudioDistortionFilter _distortionFilter = _voice.Distortion;
            if (_distortionFilter != null) {
                if (_distortion) _distortionFilter.distortionLevel = _preset.DistortionLevel;
                _distortionFilter.enabled = _distortion;
            }

            bool _echo = _hasPreset && _preset.Echo;
            AudioEchoFilter _echoFilter = _voice.Echo;
            if (_echoFilter != null) {
                if (_echo) {
                    _echoFilter.delay = _preset.EchoDelay;
                    _echoFilter.decayRatio = _preset.EchoDecay;
                    _echoFilter.wetMix = _preset.EchoWet;
                    _echoFilter.dryMix = 1f;
                }
                _echoFilter.enabled = _echo;
            }

            _voice.Source.outputAudioMixerGroup = _hasPreset && _preset.Output != null ? _preset.Output : _defaultOutput;
            _voice.SetEffectCutoffs(_lowPass, _lowPass ? _preset.LowPassCutoff : 0f, _highPass, _highPass ? _preset.HighPassCutoff : 0f);
        }
    }
}
