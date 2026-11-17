using System.Collections.Generic;
using System.IO;
using HarryTools.DomainReload;
using UnityEditor;
using UnityEngine;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Finds, or creates on demand, the project's <see cref="DialogueSettings"/> asset </summary>
    internal static class DialogueSettingsLocator {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string AssetFolder = "Assets/HarryTools/Dialogue/Resources";
        private const string AssetPath = AssetFolder + "/" + DialogueSettings.ResourcesName + ".asset";
        #endregion

        /// <summary> When set, <see cref="Find"/> and <see cref="GetOrCreate"/> return it instead of touching the project </summary>
        [ClearOnReload] internal static DialogueSettings OverrideForTests;

        // ==================== METHODS ====================
        #region Methods
        /// <summary> The project's settings, or null when there is none. Never creates anything </summary>
        internal static DialogueSettings Find() {
            if (OverrideForTests != null) return OverrideForTests;

            DialogueSettings _atDefault = AssetDatabase.LoadAssetAtPath<DialogueSettings>(AssetPath);
            if (_atDefault != null) return _atDefault;

            foreach (string _guid in AssetDatabase.FindAssets("t:DialogueSettings")) {
                DialogueSettings _found = AssetDatabase.LoadAssetAtPath<DialogueSettings>(AssetDatabase.GUIDToAssetPath(_guid));
                if (_found != null) return _found;
            }
            return null;
        }

        /// <summary> The project's settings; when missing, creates them at the default path </summary>
        internal static DialogueSettings GetOrCreate() {
            // Runtime reads made before the asset existed cached the in-memory defaults
            DialogueSettings.ForgetFallback();

            DialogueSettings _existing = Find();
            if (_existing != null) return _existing;

            Directory.CreateDirectory(AssetFolder);
            DialogueSettings _created = ScriptableObject.CreateInstance<DialogueSettings>();
            AssetDatabase.CreateAsset(_created, AssetPath);
            AssetDatabase.SaveAssets();
            return _created;
        }
        #endregion
    }

    /// <summary> Project Settings page for the dialogue defaults. Creates the settings asset on first open </summary>
    internal static class DialogueSettingsProvider {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string SettingsPath = "Project/HarryTools/Dialogue";

        private static UnityEditor.Editor cachedEditor;
        #endregion

        // ==================== PROVIDER ====================
        #region Provider
        [SettingsProvider]
        private static SettingsProvider Create() {
            return new SettingsProvider(SettingsPath, SettingsScope.Project) {
                label = "Dialogue",
                keywords = new HashSet<string> { "dialogue", "conversation", "line", "choice", "history", "localization" },
                guiHandler = _ => Draw(),
                deactivateHandler = () => {
                    if (cachedEditor != null) Object.DestroyImmediate(cachedEditor);
                    cachedEditor = null;
                }
            };
        }

        [MenuItem("Tools/HarryTools/Dialogue/Open Settings")]
        private static void OpenSettings() {
            SettingsService.OpenProjectSettings(SettingsPath);
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        private static void Draw() {
            if (EditorApplication.isPlayingOrWillChangePlaymode && DialogueSettingsLocator.Find() == null) {
                EditorGUILayout.HelpBox("The settings asset is created when this page is opened outside Play mode.", MessageType.Info);
                return;
            }

            DialogueSettings _settings = DialogueSettingsLocator.GetOrCreate();
            UnityEditor.Editor.CreateCachedEditor(_settings, null, ref cachedEditor);
            cachedEditor.OnInspectorGUI();
        }
        #endregion
    }
}
