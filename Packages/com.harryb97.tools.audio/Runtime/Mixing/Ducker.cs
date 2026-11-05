using System.Collections.Generic;
using UnityEngine;

namespace HarryTools.Audio {
    /// <summary>
    /// Lowers target buses while their trigger bus has active voices. Each rule moves its own offset linearly in dB
    /// (Attack going down, Release going up, over the whole AttenuationDb range); a bus with several rules gets the most negative one.
    /// </summary>
    internal sealed class Ducker {
        // ==================== VARIABLES ===================
        private static readonly int BusCount = BusMixer.BusCount;

        private readonly DuckingRule[] rules;
        private readonly float[] current;
        private readonly bool[] triggerActive = new bool[BusCount];
        private readonly bool[] external = new bool[BusCount];
        private readonly float[] applied = new float[BusCount];
        private readonly bool[] hasTarget = new bool[BusCount];
        private readonly BusMixer mixer;

        // ==================== CONSTRUCTOR ===================
        public Ducker(IReadOnlyList<DuckingRule> _rules, BusMixer _mixer) {
            mixer = _mixer;
            int _count = _rules != null ? _rules.Count : 0;
            rules = new DuckingRule[_count];
            current = new float[_count];
            for (int _i = 0; _i < _count; _i++) {
                rules[_i] = _rules[_i];
                int _target = (int)rules[_i].Target;
                if ((uint)_target < (uint)BusCount) hasTarget[_target] = true;
            }
        }

        // ==================== PUBLIC ===================
        /// <summary> Lets something that is not a voice of the pool (the music player) count as activity of a bus. </summary>
        internal void SetExternalActivity(AudioBus _bus, bool _active) => external[(int)_bus] = _active;

        /// <summary> Current offset of a rule in dB (0 or negative). </summary>
        public float GetRuleOffset(int _index) => current[_index];

        public void Tick(float _dt, VoiceManager _voices) {
            if (rules.Length == 0) return;
            for (int _i = 0; _i < BusCount; _i++) triggerActive[_i] = external[_i];
            IReadOnlyList<AudioVoice> _active = _voices.ActiveVoices;
            for (int _i = 0; _i < _active.Count; _i++) {
                AudioVoice _voice = _active[_i];
                if (_voice == null || _voice.IsFrozen || _voice.InDelay) continue;
                triggerActive[(int)_voice.Bus] = true;
            }

            for (int _i = 0; _i < rules.Length; _i++) {
                DuckingRule _rule = rules[_i];
                int _trigger = (int)_rule.Trigger;
                float _range = Mathf.Min(0f, _rule.AttenuationDb);
                float _goal = (uint)_trigger < (uint)BusCount && triggerActive[_trigger] ? _range : 0f;
                float _value = current[_i];
                if (_goal < _value) {
                    float _step = _rule.Attack > 0f ? -_range / _rule.Attack * _dt : float.MaxValue;
                    _value = Mathf.Max(_goal, _value - _step);
                } else if (_goal > _value) {
                    float _step = _rule.Release > 0f ? -_range / _rule.Release * _dt : float.MaxValue;
                    _value = Mathf.Min(_goal, _value + _step);
                }
                current[_i] = _value;
            }

            for (int _bus = 0; _bus < BusCount; _bus++) {
                if (!hasTarget[_bus]) continue;
                float _offset = 0f;
                for (int _i = 0; _i < rules.Length; _i++) {
                    if ((int)rules[_i].Target == _bus && current[_i] < _offset) _offset = current[_i];
                }
                if (_offset == applied[_bus]) continue;
                applied[_bus] = _offset;
                mixer.SetDuckOffsetDb((AudioBus)_bus, _offset);
            }
        }

        /// <summary> Removes every offset. Called when the runtime shuts down so the mixer is left clean. </summary>
        public void Reset() {
            for (int _i = 0; _i < current.Length; _i++) current[_i] = 0f;
            for (int _bus = 0; _bus < BusCount; _bus++) {
                if (!hasTarget[_bus]) continue;
                applied[_bus] = 0f;
                mixer.SetDuckOffsetDb((AudioBus)_bus, 0f);
            }
        }
    }
}
