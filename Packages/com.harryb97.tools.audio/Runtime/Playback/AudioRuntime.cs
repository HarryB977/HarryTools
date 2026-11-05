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
        /// <summary> Seconds without volume changes before they are saved. </summary>
        public const float SaveDelay = 0.5f;

        private static readonly int BusCount = BusMixer.BusCount;

        private readonly GameObject host;
        private readonly ILoopable loop;
        private readonly LoopSystem.LoopDelegate tick;
        private readonly AudioMixerGroup[] busGroups = new AudioMixerGroup[BusCount];
        private readonly Action<AudioHandle> onStarted;
        private readonly Action<AudioHandle> onEnded;
        private bool disposed;
        private bool saveDirty;
        private float lastChangeTime;

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
            Ducker = new Ducker(_settings.Ducking, Mixer);

            AudioVoice _template = AudioVoice.CreateTemplate(host.transform);
            Voices = new VoiceManager(this, _template, host.transform, _settings.MaxVoices, _settings.PrewarmVoices);
            Music = new MusicChannel(this, host.transform);
            VoiceChannel = new VoiceChannel(this);

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
        public MusicChannel Music { get; }
        /// <summary> Voice lines, emitter queues and conversations. Null until the constructor creates it. </summary>
        public VoiceChannel VoiceChannel { get; }
        public AudioListenerTracker ListenerTracker { get; }
        public Ducker Ducker { get; }
        public IAudioSettingsStore Store { get; private set; }
        public bool IsDisposed => disposed;
        /// <summary> False when the host object was destroyed from outside; the runtime must then be rebuilt. </summary>
        public bool IsHostAlive => host != null;
        /// <summary> The "HarryTools Audio" object that owns the voices. </summary>
        public GameObject Host => host;
        /// <summary> True while a volume change is waiting to be saved. </summary>
        public bool HasPendingSave => saveDirty;

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

        /// <summary> Schedules a save: the tick writes the volumes once SaveDelay seconds pass without another change. </summary>
        public void MarkVolumesChanged() {
            saveDirty = true;
            lastChangeTime = Time.unscaledTime;
        }

        /// <summary> Saves the current bus volumes to the store now, if a change is pending. </summary>
        public void FlushSave() {
            if (!saveDirty) return;
            saveDirty = false;
            Store.Save(Mixer.Capture()).Forget();
        }

        // A throwing subscriber must not abort the release loop of a tick or make Play throw after the voice started.
        public void RaiseStarted(AudioHandle _handle) {
            if (onStarted == null) return;
            try { onStarted(_handle); } catch (Exception _exception) { Debug.LogException(_exception); }
        }

        public void RaiseEnded(AudioHandle _handle) {
            if (onEnded == null) return;
            try { onEnded(_handle); } catch (Exception _exception) { Debug.LogException(_exception); }
        }

        public void Dispose() {
            if (disposed) return;
            disposed = true;
            FlushSave();
            if (LoopSystem.TryGetLoop(LoopId, out ILoopable _registered) && _registered == loop) LoopSystem.RemoveLoop(LoopId);
            loop.RemoveUpdateListener(tick);
            VoiceChannel.Dispose();
            Voices.Dispose();
            Music.Dispose();
            Ducker.Reset();
            if (host != null) {
                if (Application.isPlaying) Object.Destroy(host);
                else Object.DestroyImmediate(host);
            }
        }

        // ==================== PRIVATE ===================
        private void Tick() {
            if (disposed) return;
            Voices.Tick(Time.unscaledDeltaTime);
            VoiceChannel.Tick();
            Music.Tick(Time.unscaledDeltaTime);
            Ducker.Tick(Time.unscaledDeltaTime, Voices);
            if (saveDirty && Time.unscaledTime - lastChangeTime >= SaveDelay) FlushSave();
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
