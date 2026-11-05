using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using HarryTools.GameLoop;
using UnityEngine;
using UnityEngine.Audio;
using Object = UnityEngine.Object;

namespace HarryTools.Audio {
    /// <summary>
    /// The live audio system: the "HarryTools Audio" host object with the voice pool, the bus mixer, the settings store
    /// and the "HarryTools.Audio" loop that ticks the voices.
    /// </summary>
    internal sealed class AudioRuntime : IDisposable {
        // ==================== VARIABLES ===================
        public const string LoopId = "HarryTools.Audio";
        public const string HostName = "HarryTools Audio";

        private static readonly int BusCount = Enum.GetValues(typeof(AudioBus)).Length;

        private readonly GameObject host;
        private readonly ILoopable loop;
        private readonly LoopSystem.LoopDelegate tick;
        private readonly AudioMixerGroup[] busGroups = new AudioMixerGroup[BusCount];
        private readonly Action<AudioHandle> onStarted;
        private readonly Action<AudioHandle> onEnded;
        private bool disposed;

        // ==================== CONSTRUCTOR ===================
        public AudioRuntime(AudioProjectSettings _settings, Action<AudioHandle> _onStarted, Action<AudioHandle> _onEnded) {
            Settings = _settings;
            onStarted = _onStarted;
            onEnded = _onEnded;
            ReadBusGroups();

            host = new GameObject(HostName);
            if (Application.isPlaying) Object.DontDestroyOnLoad(host);
            host.hideFlags = Application.isPlaying ? HideFlags.NotEditable : HideFlags.HideAndDontSave;

            Mixer = new BusMixer(_settings);
            Mixer.Apply();
            ListenerTracker = new AudioListenerTracker();

            AudioVoice _template = AudioVoice.CreateTemplate(host.transform);
            Voices = new VoiceManager(this, _template, host.transform, _settings.MaxVoices, _settings.PrewarmVoices);

            tick = Tick;
            if (LoopSystem.HasLoop(LoopId)) LoopSystem.RemoveLoop(LoopId);
            loop = new Loop(LoopId);
            loop.AddUpdateListener(tick);
            LoopSystem.AddLoop(loop);

            Store = new PlayerPrefsAudioStore(_settings.PersistenceKey);
            LoadFromStore().Forget();
        }

        // ==================== PROPERTIES ===================
        public AudioProjectSettings Settings { get; }
        public BusMixer Mixer { get; }
        public VoiceManager Voices { get; }
        public AudioListenerTracker ListenerTracker { get; }
        public IAudioSettingsStore Store { get; private set; }
        public bool IsDisposed => disposed;

        /// <summary> True when bus volumes must be multiplied on the audio sources: no mixer on this platform or none assigned. </summary>
        public bool MultiplyOnSources => !AudioPlatform.UsesMixer || Settings.Mixer == null;

        // ==================== PUBLIC ===================
        /// <summary> Mixer group for the asset: its OutputOverride, else its bus group (Master when the bus has none). Null without mixer. </summary>
        public AudioMixerGroup GetOutput(AudioAsset _asset) {
            if (!AudioPlatform.UsesMixer) return null;
            if (_asset.OutputOverride != null) return _asset.OutputOverride;
            AudioMixerGroup _group = busGroups[(int)_asset.Bus];
            return _group != null ? _group : busGroups[(int)AudioBus.Master];
        }

        /// <summary> Replaces the store and loads the saved volumes from it. Null goes back to PlayerPrefs. </summary>
        public void SetStore(IAudioSettingsStore _store) {
            Store = _store ?? new PlayerPrefsAudioStore(Settings.PersistenceKey);
            LoadFromStore().Forget();
        }

        /// <summary> Saves the current bus volumes to the store. </summary>
        public void Save() => Store.Save(Mixer.Capture()).Forget();

        public void RaiseStarted(AudioHandle _handle) => onStarted?.Invoke(_handle);
        public void RaiseEnded(AudioHandle _handle) => onEnded?.Invoke(_handle);

        public void Dispose() {
            if (disposed) return;
            disposed = true;
            if (LoopSystem.TryGetLoop(LoopId, out ILoopable _registered) && _registered == loop) LoopSystem.RemoveLoop(LoopId);
            loop.RemoveUpdateListener(tick);
            Voices.Dispose();
            if (host != null) {
                if (Application.isPlaying) Object.Destroy(host);
                else Object.DestroyImmediate(host);
            }
        }

        // ==================== PRIVATE ===================
        private void Tick() {
            if (disposed) return;
            Voices.Tick(Time.unscaledDeltaTime);
        }

        private void ReadBusGroups() {
            IReadOnlyList<BusConfig> _buses = Settings.Buses;
            for (int _i = 0; _i < _buses.Count; _i++) {
                int _index = (int)_buses[_i].Bus;
                if ((uint)_index < (uint)BusCount) busGroups[_index] = _buses[_i].Group;
            }
        }

        private async UniTaskVoid LoadFromStore() {
            IAudioSettingsStore _store = Store;
            AudioVolumeData? _data = await _store.Load();
            // Ignore a load that finished after shutdown or after the store was replaced.
            if (disposed || _store != Store || !_data.HasValue) return;
            Mixer.Restore(_data.Value);
        }
    }
}
