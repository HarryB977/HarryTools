using System;
using System.Collections.Generic;
using System.IO;
using HarryTools.DomainReload;
using UnityEditor;
using UnityEditor.Localization.Plugins.Google;
using UnityEngine;
using Object = UnityEngine.Object;

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
        private const string SheetsUndoName = "Edit Dialogue Sheets Settings";

        private static UnityEditor.Editor cachedEditor;
        private static string connectionMessage;
        private static MessageType connectionMessageType;
        #endregion

        // ==================== PROVIDER ====================
        #region Provider
        [SettingsProvider]
        private static SettingsProvider Create() {
            return new SettingsProvider(SettingsPath, SettingsScope.Project) {
                label = "Dialogue",
                keywords = new HashSet<string> { "dialogue", "conversation", "line", "choice", "history", "localization", "google", "sheets" },
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

            EditorGUILayout.Space();
            DrawSheets();
        }

        private static void DrawSheets() {
            DialogueSheetsSettings _sheets = DialogueSheetsSettings.instance;
            EditorGUILayout.LabelField("Google Sheets", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            SheetsServiceProvider _provider = (SheetsServiceProvider)EditorGUILayout.ObjectField(
                new GUIContent("Service Provider", "Asset with the Google credentials. Keep it outside Resources so it never reaches a build"),
                _sheets.Provider, typeof(SheetsServiceProvider), false);
            string _spreadsheetId = EditorGUILayout.TextField(new GUIContent("Spreadsheet ID", "The ID in the spreadsheet URL, between /d/ and /edit"), _sheets.SpreadsheetId);
            if (EditorGUI.EndChangeCheck()) {
                Undo.RecordObject(_sheets, SheetsUndoName);
                _sheets.Provider = _provider;
                _sheets.SpreadsheetId = _spreadsheetId == null ? null : _spreadsheetId.Trim();
                EditorUtility.SetDirty(_sheets);
                _sheets.Save();
                connectionMessage = null;
            }

            if (_sheets.Provider != null && AssetDatabase.GetAssetPath(_sheets.Provider).IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase) >= 0) {
                EditorGUILayout.HelpBox("The service provider is inside a Resources folder, so its credentials would be included in builds.", MessageType.Warning);
            }

            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode)) {
                if (GUILayout.Button("Test connection")) {
                    string _error = DialogueSheetMenu.TestConnection();
                    connectionMessage = _error ?? "Connected.";
                    connectionMessageType = _error == null ? MessageType.Info : MessageType.Error;
                }

                // Run after this repaint, so their dialogs and progress bar stay out of the settings layout
                using (new EditorGUILayout.HorizontalScope()) {
                    if (GUILayout.Button(new GUIContent("Push all", "Pull, then push every dialogue and the characters"))) EditorApplication.delayCall += () => DialogueSheetMenu.SyncAll(true);
                    if (GUILayout.Button(new GUIContent("Pull all", "Pull every dialogue and the characters"))) EditorApplication.delayCall += () => DialogueSheetMenu.SyncAll(false);
                }
                using (new EditorGUILayout.HorizontalScope()) {
                    if (GUILayout.Button(new GUIContent("Push characters", "Pull, then push the character names"))) EditorApplication.delayCall += () => DialogueSheetMenu.SyncCharacters(true);
                    if (GUILayout.Button(new GUIContent("Pull characters", "Pull the character names"))) EditorApplication.delayCall += () => DialogueSheetMenu.SyncCharacters(false);
                }
            }
            if (!string.IsNullOrEmpty(connectionMessage)) EditorGUILayout.HelpBox(connectionMessage, connectionMessageType);
        }
        #endregion
    }
}
