using UnityEditor;
using UnityEngine;

namespace HarryTools.Audio.Editor {
    /// <summary> Plays an audio asset in the editor through a hidden AudioSource. Delay, cooldown, instance limits and spatial settings are ignored. </summary>
    internal static class AudioPreviewer {
        // ==================== VARIABLES ===================
        private static GameObject host;
        private static AudioSource source;
        private static AudioAsset current;

        // ==================== PROPERTIES ===================
        public static bool IsPlaying => host != null && source != null && source.isPlaying;
        /// <summary> Asset being previewed, or null when nothing plays. </summary>
        public static AudioAsset Current => IsPlaying ? current : null;

        // ==================== PUBLIC ===================
        public static void Play(AudioAsset _asset) {
            Stop();
            if (_asset == null) return;
            AudioClip _clip = new ClipSelector(_asset, new System.Random()).Next();
            if (_clip == null) {
                HarryTools.LoggerTool.Logger.LogWarning("Audio", $"{_asset.name} has no clip to preview.");
                return;
            }

            host = new GameObject("HarryTools Audio Preview") { hideFlags = HideFlags.HideAndDontSave };
            source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.clip = _clip;
            source.loop = _asset.Loop;
            source.volume = Random.Range(_asset.VolumeRange.x, _asset.VolumeRange.y);
            source.pitch = Random.Range(_asset.PitchRange.x, _asset.PitchRange.y);
            source.outputAudioMixerGroup = ResolveGroup(_asset);
            current = _asset;
            source.Play();
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        public static void Stop() {
            EditorApplication.update -= Tick;
            if (source != null) source.Stop();
            if (host != null) Object.DestroyImmediate(host);
            host = null;
            source = null;
            current = null;
        }

        // ==================== PRIVATE ===================
        private static UnityEngine.Audio.AudioMixerGroup ResolveGroup(AudioAsset _asset) {
            if (_asset.OutputOverride != null) return _asset.OutputOverride;
            AudioProjectSettings _settings = AudioSettingsProvider.FindSettings();
            if (_settings == null) return null;
            for (int _i = 0; _i < _settings.Buses.Count; _i++) {
                if (_settings.Buses[_i].Bus == _asset.Bus) return _settings.Buses[_i].Group;
            }
            return null;
        }

        private static void Tick() {
            if (host != null && source != null && source.isPlaying) return;
            Stop();
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        [InitializeOnLoadMethod]
        private static void Register() {
            EditorApplication.playModeStateChanged += _ => Stop();
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;
        }
    }
}
