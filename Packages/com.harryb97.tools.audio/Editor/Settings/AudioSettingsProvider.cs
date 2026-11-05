using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace HarryTools.Audio.Editor {
    /// <summary> Project Settings page for the audio settings asset. Creates the asset, a default catalog and the bus mixer link on first open. </summary>
    internal static class AudioSettingsProvider {
        // ==================== VARIABLES ===================
        internal const string SettingsPath = "Assets/Resources/HarryTools/AudioProjectSettings.asset";
        internal const string DefaultCatalogPath = "Assets/HarryTools/Audio/AudioCatalog.asset";
        internal const string DefaultMixerPath = "Packages/com.harryb97.tools.audio/Runtime/Mixer/HarryTools Audio Mixer.mixer";

        private static SerializedObject serialized;
        private static AudioProjectSettings serializedTarget;

        // ==================== INTERNAL ===================
        /// <summary> The settings asset if the project has one; never creates it. </summary>
        internal static AudioProjectSettings FindSettings() => AssetDatabase.LoadAssetAtPath<AudioProjectSettings>(SettingsPath);

        /// <summary> The settings asset, created with a default catalog and mixer when missing. </summary>
        internal static AudioProjectSettings GetOrCreateSettings() {
            var _settings = FindSettings();
            if (_settings != null) return _settings;

            EnsureFolder(Path.GetDirectoryName(SettingsPath).Replace('\\', '/'));
            _settings = ScriptableObject.CreateInstance<AudioProjectSettings>();
            _settings.Mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(DefaultMixerPath);
            AssignGroups(_settings);
            AssetDatabase.CreateAsset(_settings, SettingsPath);

            var _catalog = AssetDatabase.LoadAssetAtPath<AudioCatalog>(DefaultCatalogPath);
            if (_catalog == null) {
                EnsureFolder(Path.GetDirectoryName(DefaultCatalogPath).Replace('\\', '/'));
                _catalog = ScriptableObject.CreateInstance<AudioCatalog>();
                AssetDatabase.CreateAsset(_catalog, DefaultCatalogPath);
            }
            _settings.Catalog = _catalog;
            EditorUtility.SetDirty(_settings);
            AssetDatabase.SaveAssets();
            AudioCatalogMaintainer.Rebuild(_catalog);
            return _settings;
        }

        // ==================== PROVIDER ===================
        [SettingsProvider]
        private static SettingsProvider Create() {
            return new SettingsProvider("Project/HarryTools/Audio", SettingsScope.Project) {
                label = "Audio",
                keywords = new HashSet<string> { "audio", "mixer", "bus", "catalog", "sound", "music", "ducking" },
                guiHandler = _ => Draw(),
                deactivateHandler = () => { serialized = null; serializedTarget = null; },
            };
        }

        [MenuItem("Tools/HarryTools/Audio/Open Settings")]
        private static void OpenSettings() => SettingsService.OpenProjectSettings("Project/HarryTools/Audio");

        // ==================== PRIVATE ===================
        private static void Draw() {
            var _settings = GetOrCreateSettings();
            if (serialized == null || serializedTarget != _settings) {
                serialized = new SerializedObject(_settings);
                serializedTarget = _settings;
            }
            serialized.Update();
            EditorGUI.BeginChangeCheck();
            SerializedProperty _property = serialized.GetIterator();
            bool _enter = true;
            while (_property.NextVisible(_enter)) {
                _enter = false;
                if (_property.propertyPath == "m_Script") continue;
                EditorGUILayout.PropertyField(_property, true);
            }
            bool _changed = EditorGUI.EndChangeCheck();
            serialized.ApplyModifiedProperties();
            if (_changed) AssignMissingGroups(_settings);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(_settings.Catalog == null)) {
                if (GUILayout.Button("Rebuild Catalog and Ids")) AudioCatalogMaintainer.Rebuild(_settings.Catalog);
            }
            if (_settings.Catalog == null) EditorGUILayout.HelpBox("Assign a catalog to resolve audio references.", MessageType.Warning);
        }

        // Fills the group of any bus left empty when the mixer has a group with the bus name.
        private static void AssignMissingGroups(AudioProjectSettings _settings) {
            if (_settings.Mixer == null) return;
            var _buses = new List<BusConfig>(_settings.Buses);
            bool _dirty = false;
            for (int _i = 0; _i < _buses.Count; _i++) {
                BusConfig _config = _buses[_i];
                if (_config.Group != null) continue;
                AudioMixerGroup[] _found = _settings.Mixer.FindMatchingGroups(_config.Bus.ToString());
                if (_found.Length == 0) continue;
                _config.Group = _found[0];
                _buses[_i] = _config;
                _dirty = true;
            }
            if (!_dirty) return;
            _settings.SetBuses(_buses);
            EditorUtility.SetDirty(_settings);
            serialized = null;
        }

        private static void AssignGroups(AudioProjectSettings _settings) => AssignMissingGroups(_settings);

        private static void EnsureFolder(string _folder) {
            if (AssetDatabase.IsValidFolder(_folder)) return;
            string _parent = Path.GetDirectoryName(_folder).Replace('\\', '/');
            EnsureFolder(_parent);
            AssetDatabase.CreateFolder(_parent, Path.GetFileName(_folder));
        }
    }
}
