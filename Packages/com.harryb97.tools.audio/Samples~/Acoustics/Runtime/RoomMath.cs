using System;
using UnityEngine;

namespace HarryTools.Audio.Acoustics {
    /// <summary> One ray traced from a room cell. </summary>
    internal struct RoomSample {
        public bool Hit;
        public float Distance;
        public Vector3 Absorption;
    }

    /// <summary> Acoustic summary of the space around a point. </summary>
    internal struct RoomEstimate {
        public float MeanFreePath;
        public float Openness;
        public Vector3 Absorption;
        public float DecayTime;
    }

    /// <summary> Pure formulas that turn room rays into reverb settings. </summary>
    internal static class RoomMath {
        // ==================== VARIABLES ===================
        private const float MinAbsorption = 0.01f;
        private const float MinDecay = 0.1f;
        private const float MaxDecay = 8f;
        private const float SpeedOfSound = 343f;
        private const float MaxReflectionsDelay = 0.3f;

        // ==================== PUBLIC API ===================
        /// <summary> Builds the room estimate from traced rays. </summary>
        public static RoomEstimate Estimate(ReadOnlySpan<RoomSample> _samples, float _maxDistance) {
            int _hits = 0;
            float _distance = 0f;
            Vector3 _absorption = Vector3.zero;
            for (int i = 0; i < _samples.Length; i++) {
                if (!_samples[i].Hit) continue;
                _hits++;
                _distance += _samples[i].Distance;
                _absorption += _samples[i].Absorption;
            }

            RoomEstimate _room = new RoomEstimate();
            if (_hits > 0) {
                _room.MeanFreePath = _distance / _hits;
                _room.Absorption = _absorption / _hits;
            } else {
                _room.MeanFreePath = _maxDistance;
            }
            _room.Openness = _samples.Length > 0 ? (_samples.Length - _hits) / (float)_samples.Length : 1f;
            _room.DecayTime = Mathf.Clamp(0.04f * _room.MeanFreePath / Mathf.Max(_room.Absorption.y, MinAbsorption), MinDecay, MaxDecay);
            return _room;
        }

        private const float OpenAirOpenness = 0.45f;

        /// <summary> Converts a room estimate into reverb. <paramref name="_wetScale"/> is the profile wet times the source send. </summary>
        public static AcousticReverb ToReverb(in RoomEstimate _room, float _wetScale) {
            // A ground plane alone leaves about half of the sphere open and must stay dry.
            float _closed = Mathf.Clamp01(1f - _room.Openness / OpenAirOpenness);
            return new AcousticReverb {
                Wet = Mathf.Clamp01(_room.DecayTime / 2f) * _closed * _closed * Mathf.Max(0f, _wetScale),
                DecayTime = Mathf.Clamp(_room.DecayTime, MinDecay, MaxDecay),
                HighFrequencyRatio = Mathf.Clamp(_room.Absorption.y / Mathf.Max(_room.Absorption.z, MinAbsorption), 0.1f, 2f),
                ReflectionsDelay = Mathf.Clamp(_room.MeanFreePath / SpeedOfSound, 0f, MaxReflectionsDelay),
            };
        }
    }
}
