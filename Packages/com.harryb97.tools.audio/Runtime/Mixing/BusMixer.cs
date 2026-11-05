using System.Collections.Generic;
using UnityEngine;

namespace HarryTools.Audio {
    /// <summary> Per-bus volume, mute and ducking. Writes to the mixer, or exposes a multiplier for the audio sources when there is no mixer. </summary>
    internal sealed class BusMixer {
        // ==================== VARIABLES ===================
        private static readonly int BusCount = System.Enum.GetValues(typeof(AudioBus)).Length;

        private readonly AudioProjectSettings settings;
        private readonly float[] volumes = new float[BusCount];
        private readonly bool[] muted = new bool[BusCount];
        private readonly float[] duckDb = new float[BusCount];
        private readonly string[] parameters = new string[BusCount];

        // ==================== CONSTRUCTOR ===================
        public BusMixer(AudioProjectSettings _settings) {
            settings = _settings;
            for (int _i = 0; _i < BusCount; _i++) volumes[_i] = 1f;
            if (_settings == null) return;
            IReadOnlyList<BusConfig> _buses = _settings.Buses;
            for (int _i = 0; _i < _buses.Count; _i++) {
                int _index = (int)_buses[_i].Bus;
                if ((uint)_index >= (uint)BusCount) continue;
                volumes[_index] = Mathf.Clamp01(_buses[_i].DefaultVolume);
                parameters[_index] = _buses[_i].VolumeParameter;
            }
        }

        // ==================== PUBLIC ===================
        public void SetVolume(AudioBus _bus, float _linear) {
            volumes[(int)_bus] = Mathf.Clamp01(_linear);
            Apply(_bus);
        }

        public float GetVolume(AudioBus _bus) => volumes[(int)_bus];

        public void SetMuted(AudioBus _bus, bool _muted) {
            muted[(int)_bus] = _muted;
            Apply(_bus);
        }

        public bool IsMuted(AudioBus _bus) => muted[(int)_bus];

        public void SetDuckOffsetDb(AudioBus _bus, float _db) {
            duckDb[(int)_bus] = _db;
            Apply(_bus);
        }

        /// <summary> Volume, mute and duck of the bus and of Master (not counted twice for Master). Meant for sources when the mixer is not used. </summary>
        public float GetSourceMultiplier(AudioBus _bus) {
            float _value = BusFactor((int)_bus);
            if (_bus != AudioBus.Master) _value *= BusFactor((int)AudioBus.Master);
            return _value;
        }

        /// <summary> Writes every bus to the mixer. Does nothing without a mixer. </summary>
        public void Apply() {
            for (int _i = 0; _i < BusCount; _i++) Apply((AudioBus)_i);
        }

        public AudioVolumeData Capture() {
            var _data = new AudioVolumeData { Volumes = new float[BusCount], Muted = new bool[BusCount] };
            System.Array.Copy(volumes, _data.Volumes, BusCount);
            System.Array.Copy(muted, _data.Muted, BusCount);
            return _data;
        }

        public void Restore(AudioVolumeData _data) {
            int _volumes = _data.Volumes != null ? Mathf.Min(_data.Volumes.Length, BusCount) : 0;
            for (int _i = 0; _i < _volumes; _i++) volumes[_i] = Mathf.Clamp01(_data.Volumes[_i]);
            int _muted = _data.Muted != null ? Mathf.Min(_data.Muted.Length, BusCount) : 0;
            for (int _i = 0; _i < _muted; _i++) muted[_i] = _data.Muted[_i];
            Apply();
        }

        // ==================== PRIVATE ===================
        private float BusFactor(int _index) => muted[_index] ? 0f : volumes[_index] * AudioMath.DbToLinear(duckDb[_index]);

        private void Apply(AudioBus _bus) {
            if (!AudioPlatform.UsesMixer || settings == null || settings.Mixer == null) return;
            int _index = (int)_bus;
            string _parameter = parameters[_index];
            if (string.IsNullOrEmpty(_parameter)) return;
            float _db = muted[_index] ? AudioMath.MinDb : Mathf.Max(AudioMath.MinDb, AudioMath.LinearToDb(volumes[_index]) + duckDb[_index]);
            settings.Mixer.SetFloat(_parameter, _db);
        }
    }
}
