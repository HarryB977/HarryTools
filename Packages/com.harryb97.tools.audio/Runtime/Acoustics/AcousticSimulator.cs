using System;
using System.Collections.Generic;
using HarryTools.DomainReload;
using UnityEngine;

namespace HarryTools.Audio {
    /// <summary> Smoothed acoustic values of one voice. Cutoffs are stored as natural logs, so they move evenly to the ear. </summary>
    internal struct AcousticState {
        // ==================== VARIABLES ===================
        public static readonly float LogMaxCutoff = Mathf.Log(AcousticResult.NeutralLowPass);
        public static readonly float LogMinCutoff = Mathf.Log(AcousticResult.NeutralHighPass);

        public float Volume;
        public float LogLowPass;
        public float LogHighPass;
        public bool HasPerceivedPosition;
        /// <summary> Smoothed reverb: Wet and ReflectionsDelay linear, decay time and high-frequency ratio as natural logs. </summary>
        public float Wet;
        public float LogDecayTime;
        public float LogHighFrequencyRatio;
        public float ReflectionsDelay;
        /// <summary> False while every value is neutral; the simulator then leaves the voice and its filters alone. </summary>
        public bool Active;
        /// <summary> Tick in which the simulator last selected the voice. </summary>
        public int Stamp;

        // ==================== PROPERTIES ===================
        public static AcousticState Neutral => new AcousticState {
            Volume = 1f,
            LogLowPass = LogMaxCutoff,
            LogHighPass = LogMinCutoff,
            LogDecayTime = 0f,
            LogHighFrequencyRatio = Mathf.Log(0.5f),
            Stamp = -1,
        };
    }

    /// <summary>
    /// Runs the registered acoustic modules for the most audible 3D voices (at most MaxSimulated) and applies the smoothed results:
    /// a volume multiplier, low-pass and high-pass cutoffs and a perceived position. It has no geometry code of its own.
    /// With no mixer (WebGL) it does nothing and leaves every voice neutral.
    /// </summary>
    internal sealed class AcousticSimulator {
        // ==================== VARIABLES ===================
        /// <summary> Time constant in seconds of the exponential smoothing (unscaled time). </summary>
        public const float SmoothingTime = 0.1f;
        // Within these distances of the target the smoothed value snaps to it, so a voice really goes back to neutral.
        private const float SnapVolume = 0.002f;
        private const float SnapLog = 0.002f;
        // A cutoff counts as neutral at or beyond these (Hz).
        private const float NeutralLowPassHz = 21999f;
        private const float NeutralHighPassHz = 11f;
        // The reverb filter is on while the smoothed wet is above this.
        internal const float ReverbOnWet = 0.001f;
        private const float SnapDelay = 0.0005f;
        private const float ReverbLevelMin = -10000f;
        private const float ReflectionsBelowReverb = 600f;
        private const float ReverbDecayMin = 0.1f;
        private const float ReverbDecayMax = 20f;
        private const float ReverbRatioMin = 0.1f;
        private const float ReverbRatioMax = 2f;
        private const float ReverbReflectionsDelayMax = 0.3f;
        private const float ReverbDelayMax = 0.1f;
        private static readonly float LogNeutralLowHz = Mathf.Log(NeutralLowPassHz);
        private static readonly float LogNeutralHighHz = Mathf.Log(NeutralHighPassHz);

        // Registered modules sorted by Order (read at registration), ties in registration order. Static so modules registered before the runtime exists work.
        [ClearOnReload(AssignNewInstance = true)] private static List<IAcousticModule> modules = new List<IAcousticModule>();
        [ClearOnReload] private static int version;

        private readonly AudioRuntime runtime;
        private readonly int maxSimulated;
        private readonly int voiceCapacity;

        // Selection and the arrays handed to the modules, all sized MaxSimulated.
        private readonly AudioVoice[] selected;
        private readonly float[] selectedScore;
        private readonly bool[] selectedFresh;
        private readonly AcousticQuery[] queries;
        private readonly AcousticResult[] results;
        private readonly AcousticResult[] evalResults;
        private readonly AcousticQuery[] freshQueries;
        private readonly AcousticResult[] freshResults;
        private readonly int[] freshRows;

        // Per voice index: which playback and tick the stored contributions belong to.
        private readonly int[] rowGeneration;
        private readonly int[] rowStamp;

        // Per module slot. The table holds the last contribution of a module to a voice: voice index * slot capacity + slot.
        private IAcousticModule[] slots = new IAcousticModule[0];
        private float[] nextDue = new float[0];
        private AcousticResult[] table = new AcousticResult[0];
        private int slotCount;
        private int slotCapacity;
        private int syncedVersion;
        private int tickId;

        // ==================== CONSTRUCTOR ===================
        public AcousticSimulator(AudioRuntime _runtime) {
            runtime = _runtime;
            maxSimulated = Mathf.Max(1, _runtime.Settings.MaxSimulated);
            voiceCapacity = Mathf.Max(1, _runtime.Settings.MaxVoices);
            selected = new AudioVoice[maxSimulated];
            selectedScore = new float[maxSimulated];
            selectedFresh = new bool[maxSimulated];
            queries = new AcousticQuery[maxSimulated];
            results = new AcousticResult[maxSimulated];
            evalResults = new AcousticResult[maxSimulated];
            freshQueries = new AcousticQuery[maxSimulated];
            freshResults = new AcousticResult[maxSimulated];
            freshRows = new int[maxSimulated];
            rowGeneration = new int[voiceCapacity];
            rowStamp = new int[voiceCapacity];
            syncedVersion = -1;
        }

        // ==================== REGISTRY ===================
        public static int RegisteredCount => modules.Count;
        public static IAcousticModule ModuleAt(int _index) => modules[_index];

        public static void Register(IAcousticModule _module) {
            if (_module == null || modules.Contains(_module)) return;
            // After the last module with an Order not greater than ours: stable for ties.
            int _order = _module.Order;
            int _at = modules.Count;
            while (_at > 0 && modules[_at - 1].Order > _order) _at--;
            modules.Insert(_at, _module);
            version++;
        }

        public static void Unregister(IAcousticModule _module) {
            if (_module != null && modules.Remove(_module)) version++;
        }

        // ==================== TICK ===================
        public void Tick(float _unscaledDelta) {
            tickId++;
            if (version != syncedVersion) Rebuild();

            IReadOnlyList<AudioVoice> _active = runtime.Voices.ActiveVoices;
            bool _mixer = AudioPlatform.UsesMixer;
            Transform _listener = runtime.ListenerTracker.Listener;
            float _alpha = 1f - Mathf.Exp(-Mathf.Max(0f, _unscaledDelta) / SmoothingTime);

            int _count = 0;
            if (_mixer && slotCount > 0 && _listener != null) {
                _count = Select(_active);
                if (_count > 0) Evaluate(_count, _listener.position);
            }
            // Do not keep references to voices that left the selection.
            for (int _i = _count; _i < maxSimulated; _i++) selected[_i] = null;

            for (int _i = 0; _i < _count; _i++) {
                AudioVoice _voice = selected[_i];
                Apply(_voice, results[_i], _alpha);
                int _index = _voice.Index;
                rowGeneration[_index] = _voice.Generation;
                rowStamp[_index] = tickId;
            }

            // Everything else drifts back to neutral: voices that left the budget, and all of them when there are no modules.
            for (int _i = 0; _i < _active.Count; _i++) {
                AudioVoice _voice = _active[_i];
                if (_voice == null) continue;
                ref AcousticState _state = ref _voice.Acoustic;
                if (!_state.Active || _state.Stamp == tickId) continue;
                if (!_mixer) {
                    _state = AcousticState.Neutral;
                    _voice.SetPerceivedPosition(false, Vector3.zero);
                } else if (!_voice.IsFrozen && !_voice.InDelay) {
                    Apply(_voice, AcousticResult.Neutral, _alpha);
                } else {
                    // Frozen: nothing advances, but an effect applied meanwhile must not undo the module filters.
                    ApplyFilters(_voice, in _state);
                }
            }
        }

        // ==================== PRIVATE ===================
        // Candidates are active, running, 3D voices. The best MaxSimulated by audibility stay in selected, best first.
        private int Select(IReadOnlyList<AudioVoice> _active) {
            VoiceManager _voices = runtime.Voices;
            int _count = 0;
            for (int _i = 0; _i < _active.Count; _i++) {
                AudioVoice _voice = _active[_i];
                if (_voice == null || _voice.Asset == null || _voice.Index >= voiceCapacity) continue;
                if (_voice.IsFrozen || _voice.InDelay || _voice.Source.spatialBlend <= 0f) continue;

                float _score = _voices.AudibilityScore(_voice);
                int _at;
                if (_count < maxSimulated) _at = _count++;
                else if (_score > selectedScore[_count - 1]) _at = _count - 1;
                else continue;

                // Insertion into the sorted prefix: equal scores keep the older voice first.
                while (_at > 0 && selectedScore[_at - 1] < _score) {
                    selected[_at] = selected[_at - 1];
                    selectedScore[_at] = selectedScore[_at - 1];
                    _at--;
                }
                selected[_at] = _voice;
                selectedScore[_at] = _score;
            }

            for (int _i = 0; _i < _count; _i++) {
                AudioVoice _voice = selected[_i];
                _voice.Acoustic.Stamp = tickId;
                // Stored contributions only count for the same playback simulated in the previous tick.
                selectedFresh[_i] = rowGeneration[_voice.Index] != _voice.Generation || rowStamp[_voice.Index] != tickId - 1;
            }
            return _count;
        }

        // Every module writes its own full result per voice into the table (from neutral); the table is then reduced in Order.
        // A module that is not due only runs for the voices without a stored result (new in the budget); the others keep theirs.
        private void Evaluate(int _count, Vector3 _listenerPosition) {
            for (int _i = 0; _i < _count; _i++) {
                AudioVoice _voice = selected[_i];
                queries[_i] = new AcousticQuery(new AudioHandle(_voice.Index, _voice.Generation), _voice.LogicalPosition, _listenerPosition, _voice.Asset);
            }

            float _now = Time.unscaledTime;
            for (int _s = 0; _s < slotCount; _s++) {
                IAcousticModule _module = slots[_s];
                if (_module == null) continue;

                bool _due = _now >= nextDue[_s];
                try {
                    if (_due) {
                        for (int _i = 0; _i < _count; _i++) evalResults[_i] = AcousticResult.Neutral;
                        _module.Evaluate(new ReadOnlySpan<AcousticQuery>(queries, 0, _count), new Span<AcousticResult>(evalResults, 0, _count));
                        for (int _i = 0; _i < _count; _i++) table[selected[_i].Index * slotCapacity + _s] = evalResults[_i];
                        nextDue[_s] = _now + Mathf.Max(0f, _module.UpdateInterval);
                    } else {
                        int _fresh = 0;
                        for (int _i = 0; _i < _count; _i++) {
                            if (!selectedFresh[_i]) continue;
                            freshQueries[_fresh] = queries[_i];
                            freshResults[_fresh] = AcousticResult.Neutral;
                            freshRows[_fresh] = selected[_i].Index * slotCapacity + _s;
                            _fresh++;
                        }
                        if (_fresh == 0) continue;
                        _module.Evaluate(new ReadOnlySpan<AcousticQuery>(freshQueries, 0, _fresh), new Span<AcousticResult>(freshResults, 0, _fresh));
                        for (int _i = 0; _i < _fresh; _i++) table[freshRows[_i]] = freshResults[_i];
                    }
                } catch (Exception _exception) {
                    // Its slot is skipped by the reduction (neutral); the other modules are unaffected.
                    Debug.LogException(_exception);
                    slots[_s] = null;
                    Unregister(_module);
                }
            }

            for (int _i = 0; _i < _count; _i++) {
                int _row = selected[_i].Index * slotCapacity;
                AcousticResult _reduced = AcousticResult.Neutral;
                for (int _s = 0; _s < slotCount; _s++) {
                    if (slots[_s] != null) Combine(ref _reduced, table[_row + _s]);
                }
                results[_i] = _reduced;
            }
        }

        // Volume multiplies, low-pass takes the lowest, high-pass the highest, the perceived position and the reverb are the last ones in Order.
        private static void Combine(ref AcousticResult _result, in AcousticResult _contribution) {
            _result.VolumeMultiplier *= _contribution.VolumeMultiplier;
            _result.LowPassCutoff = Mathf.Min(_result.LowPassCutoff, _contribution.LowPassCutoff);
            _result.HighPassCutoff = Mathf.Max(_result.HighPassCutoff, _contribution.HighPassCutoff);
            if (_contribution.HasPerceivedPosition) {
                _result.HasPerceivedPosition = true;
                _result.PerceivedPosition = _contribution.PerceivedPosition;
            }
            if (_contribution.HasReverb) {
                _result.HasReverb = true;
                _result.Reverb = _contribution.Reverb;
            }
        }

        // Moves the smoothed state of the voice towards the target and writes it to the voice and its filters.
        private void Apply(AudioVoice _voice, in AcousticResult _target, float _alpha) {
            ref AcousticState _state = ref _voice.Acoustic;
            bool _wasActive = _state.Active;

            float _volume = Mathf.Max(0f, _target.VolumeMultiplier);
            float _low = Mathf.Log(Mathf.Clamp(_target.LowPassCutoff, AcousticResult.NeutralHighPass, AcousticResult.NeutralLowPass));
            float _high = Mathf.Log(Mathf.Clamp(_target.HighPassCutoff, AcousticResult.NeutralHighPass, AcousticResult.NeutralLowPass));
            _state.Volume = Approach(_state.Volume, _volume, _alpha, SnapVolume);
            _state.LogLowPass = Approach(_state.LogLowPass, _low, _alpha, SnapLog);
            _state.LogHighPass = Approach(_state.LogHighPass, _high, _alpha, SnapLog);

            bool _perceived = _target.HasPerceivedPosition;
            if (_perceived || _state.HasPerceivedPosition) _voice.SetPerceivedPosition(_perceived, _target.PerceivedPosition);
            _state.HasPerceivedPosition = _perceived;

            // Without reverb the wet drifts to 0 and the other values stay where they were. A reverb that starts from off takes its values at once.
            float _wet = 0f;
            if (_target.HasReverb) {
                AcousticReverb _reverb = _target.Reverb;
                _wet = Mathf.Clamp01(_reverb.Wet);
                float _logDecay = Mathf.Log(Mathf.Clamp(_reverb.DecayTime, ReverbDecayMin, ReverbDecayMax));
                float _logRatio = Mathf.Log(Mathf.Clamp(_reverb.HighFrequencyRatio, ReverbRatioMin, ReverbRatioMax));
                float _delay = Mathf.Clamp(_reverb.ReflectionsDelay, 0f, ReverbReflectionsDelayMax);
                float _shape = _state.Wet > ReverbOnWet ? _alpha : 1f;
                _state.LogDecayTime = Approach(_state.LogDecayTime, _logDecay, _shape, SnapLog);
                _state.LogHighFrequencyRatio = Approach(_state.LogHighFrequencyRatio, _logRatio, _shape, SnapLog);
                _state.ReflectionsDelay = Approach(_state.ReflectionsDelay, _delay, _shape, SnapDelay);
            }
            _state.Wet = Approach(_state.Wet, _wet, _alpha, SnapVolume);

            _state.Active = _state.Volume != 1f || _state.LogLowPass < LogNeutralLowHz || _state.LogHighPass > LogNeutralHighHz || _perceived || _state.Wet > ReverbOnWet;
            if (_wasActive || _state.Active) ApplyFilters(_voice, in _state);
        }

        private static float Approach(float _current, float _target, float _alpha, float _snap) {
            float _next = _current + (_target - _current) * _alpha;
            return Mathf.Abs(_next - _target) <= _snap ? _target : _next;
        }

        // Low-pass: lowest of the preset cutoff and the module cutoff. High-pass: highest. A filter is on while either side needs it.
        private static void ApplyFilters(AudioVoice _voice, in AcousticState _state) {
            AudioReverbFilter _reverbFilter = _voice.Reverb;
            if (_reverbFilter != null) {
                bool _on = _state.Wet > ReverbOnWet;
                if (_on) {
                    MapReverb(new AcousticReverb {
                        Wet = _state.Wet,
                        DecayTime = Mathf.Exp(_state.LogDecayTime),
                        HighFrequencyRatio = Mathf.Exp(_state.LogHighFrequencyRatio),
                        ReflectionsDelay = _state.ReflectionsDelay,
                    }, _reverbFilter);
                }
                if (_reverbFilter.enabled != _on) _reverbFilter.enabled = _on;
            }

            AudioLowPassFilter _lowFilter = _voice.LowPass;
            if (_lowFilter != null) {
                bool _module = _state.LogLowPass < LogNeutralLowHz;
                bool _on = _voice.HasEffectLowPass || _module;
                if (_on) {
                    float _cutoff = _module ? Mathf.Exp(_state.LogLowPass) : AcousticResult.NeutralLowPass;
                    if (_voice.HasEffectLowPass) _cutoff = Mathf.Min(_cutoff, _voice.EffectLowPassCutoff);
                    _lowFilter.cutoffFrequency = _cutoff;
                }
                if (_lowFilter.enabled != _on) _lowFilter.enabled = _on;
            }

            AudioHighPassFilter _highFilter = _voice.HighPass;
            if (_highFilter != null) {
                bool _module = _state.LogHighPass > LogNeutralHighHz;
                bool _on = _voice.HasEffectHighPass || _module;
                if (_on) {
                    float _cutoff = _module ? Mathf.Exp(_state.LogHighPass) : AcousticResult.NeutralHighPass;
                    if (_voice.HasEffectHighPass) _cutoff = Mathf.Max(_cutoff, _voice.EffectHighPassCutoff);
                    _highFilter.cutoffFrequency = _cutoff;
                }
                if (_highFilter.enabled != _on) _highFilter.enabled = _on;
            }
        }

        // Writes a reverb to an AudioReverbFilter: dry untouched, the wet as the reverb level, the reflections a bit below it.
        internal static void MapReverb(in AcousticReverb _reverb, AudioReverbFilter _filter) {
            if (_filter.reverbPreset != AudioReverbPreset.User) _filter.reverbPreset = AudioReverbPreset.User;
            float _level = Mathf.Clamp(2000f * Mathf.Log10(Mathf.Max(_reverb.Wet, 0.00001f)), ReverbLevelMin, 0f);
            _filter.dryLevel = 0f;
            _filter.room = 0f;
            _filter.reverbLevel = _level;
            _filter.reflectionsLevel = Mathf.Clamp(_level - ReflectionsBelowReverb, ReverbLevelMin, 0f);
            _filter.decayTime = Mathf.Clamp(_reverb.DecayTime, ReverbDecayMin, ReverbDecayMax);
            _filter.decayHFRatio = Mathf.Clamp(_reverb.HighFrequencyRatio, ReverbRatioMin, ReverbRatioMax);
            float _reflectionsDelay = Mathf.Clamp(_reverb.ReflectionsDelay, 0f, ReverbReflectionsDelayMax);
            _filter.reflectionsDelay = _reflectionsDelay;
            _filter.reverbDelay = Mathf.Clamp(_reflectionsDelay * 0.5f, 0f, ReverbDelayMax);
        }

        // Copies the registry into the slots. Storage only grows here, so a tick without registry changes never allocates.
        private void Rebuild() {
            syncedVersion = version;
            slotCount = modules.Count;
            if (slotCount > slotCapacity) {
                slotCapacity = slotCount;
                slots = new IAcousticModule[slotCapacity];
                nextDue = new float[slotCapacity];
                table = new AcousticResult[voiceCapacity * slotCapacity];
            }
            for (int _s = 0; _s < slotCapacity; _s++) {
                slots[_s] = _s < slotCount ? modules[_s] : null;
                nextDue[_s] = 0f;
            }
            // Slots moved: every stored contribution is stale.
            Array.Fill(rowStamp, -1);
        }
    }
}
