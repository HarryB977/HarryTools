using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HarryTools.TextEffects.Editor {
    /// <summary> Project Settings page for the text effects registry. Creates the settings asset on first open </summary>
    internal static class TextEffectsSettingsProvider {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string SettingsPath = "Project/HarryTools/Text Effects";

        private static UnityEditor.Editor cachedEditor;
        #endregion

        // ==================== PROVIDER ====================
        #region Provider
        [SettingsProvider]
        private static SettingsProvider Create() {
            return new SettingsProvider(SettingsPath, SettingsScope.Project) {
                label = "Text Effects",
                keywords = new HashSet<string> { "text", "effects", "typewriter", "preset", "voice", "tag", "dialogue" },
                guiHandler = _ => Draw(),
                deactivateHandler = () => {
                    if (cachedEditor != null) Object.DestroyImmediate(cachedEditor);
                    cachedEditor = null;
                }
            };
        }

        [MenuItem("Tools/HarryTools/Text Effects/Open Settings")]
        private static void OpenSettings() {
            SettingsService.OpenProjectSettings(SettingsPath);
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        private static void Draw() {
            if (EditorApplication.isPlayingOrWillChangePlaymode && TextEffectsSettingsLocator.Find() == null) {
                EditorGUILayout.HelpBox("The settings asset is created when this page is opened outside Play mode.", MessageType.Info);
                return;
            }

            TextEffectsSettings _settings = TextEffectsSettingsLocator.GetOrCreate();
            UnityEditor.Editor.CreateCachedEditor(_settings, null, ref cachedEditor);
            cachedEditor.OnInspectorGUI();

            IReadOnlyList<TextIssue> _issues = _settings.RegistryIssues;
            if (_issues.Count == 0) return;
            EditorGUILayout.Space();
            for (int i = 0; i < _issues.Count; i++) {
                EditorGUILayout.HelpBox(_issues[i].Message, _issues[i].Severity == IssueSeverity.Error ? MessageType.Error : MessageType.Warning);
            }
        }
        #endregion
    }
}
