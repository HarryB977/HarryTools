using System.IO;
using HarryTools.DomainReload;
using UnityEditor;
using UnityEngine;

namespace HarryTools.TextEffects.Editor {
    /// <summary> Finds, or creates on demand, the project's <see cref="TextEffectsSettings"/> asset </summary>
    internal static class TextEffectsSettingsLocator {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string AssetFolder = "Assets/HarryTools/TextEffects/Resources";
        private const string AssetPath = AssetFolder + "/" + TextEffectsSettings.ResourcesName + ".asset";
        #endregion

        /// <summary> When set, <see cref="Find"/> and <see cref="GetOrCreate"/> return it instead of touching the project </summary>
        [ClearOnReload] internal static TextEffectsSettings OverrideForTests;

        // ==================== METHODS ====================
        #region Methods
        /// <summary> The project's settings, or null when there is none. Never creates anything </summary>
        internal static TextEffectsSettings Find() {
            if (OverrideForTests != null) return OverrideForTests;

            TextEffectsSettings _atDefault = AssetDatabase.LoadAssetAtPath<TextEffectsSettings>(AssetPath);
            if (_atDefault != null) return _atDefault;

            foreach (string _guid in AssetDatabase.FindAssets("t:TextEffectsSettings")) {
                TextEffectsSettings _found = AssetDatabase.LoadAssetAtPath<TextEffectsSettings>(AssetDatabase.GUIDToAssetPath(_guid));
                if (_found != null) return _found;
            }
            return null;
        }

        /// <summary> The project's settings; when missing, creates them at the default path and registers every preset and voice in the project </summary>
        internal static TextEffectsSettings GetOrCreate() {
            TextEffectsSettings _existing = Find();
            if (_existing != null) return _existing;

            Directory.CreateDirectory(AssetFolder);
            TextEffectsSettings _created = ScriptableObject.CreateInstance<TextEffectsSettings>();
            AssetDatabase.CreateAsset(_created, AssetPath);

            TextEffectsRegistryMaintainer.Sync(_created, TextEffectsRegistryMaintainer.FindAllPresets(), TextEffectsRegistryMaintainer.FindAllVoices());
            _created.DefaultAppearAsset = FindByTag(_created, "fade");
            _created.DefaultDisappearAsset = FindByTag(_created, "fadeout");
            EditorUtility.SetDirty(_created);
            AssetDatabase.SaveAssets();
            return _created;
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        private static TextEffectPreset FindByTag(TextEffectsSettings _settings, string _tag) {
            return _settings.TryGetPreset(_tag, out TextEffectPreset _preset) ? _preset : null;
        }
        #endregion
    }
}
