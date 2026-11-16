using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HarryTools.TextEffects.Editor {
    /// <summary> EffectText inspector: the text field of the chosen source, markup validation and an edit-mode preview </summary>
    [CustomEditor(typeof(EffectText))]
    internal sealed class EffectTextEditor : UnityEditor.Editor {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string ScriptField = "m_Script";
        private const string SourceModeField = "sourceMode";
        private const string PlainTextField = "plainText";
        private const string LocalizedTextField = "localizedText";

        /// <summary> Seconds between checks for changes made outside this inspector: string tables, settings, scripts </summary>
        private const double ExternalRefreshInterval = 1.0;

        private readonly List<TextIssue> textIssues = new List<TextIssue>();
        private readonly List<LocaleIssue> localeIssues = new List<LocaleIssue>();
        private readonly Dictionary<string, string> textByLocale = new Dictionary<string, string>();
        private readonly List<string> localeCodes = new List<string>();

        private SerializedProperty sourceMode;
        private EffectTextPreview preview;
        private bool settingsMissing;
        private bool localizedMissing;
        private bool issuesDirty = true;
        private double lastRefreshTime;
        private string selectedLocale;
        private Vector2 eventScroll;

        // Preview state read on the layout pass, so an editor update between layout and repaint does not change the controls
        private bool previewShown;
        private int eventsShown;
        #endregion

        // ==================== EDITOR ===================
        #region Unity Callbacks
        private void OnEnable() {
            sourceMode = serializedObject.FindProperty(SourceModeField);
            if (targets.Length == 1) preview = new EffectTextPreview((EffectText)target);
            issuesDirty = true;
            Undo.undoRedoPerformed += MarkIssuesDirty;
        }

        private void OnDisable() {
            Undo.undoRedoPerformed -= MarkIssuesDirty;
            preview?.Stop();
        }

        public override bool RequiresConstantRepaint() {
            return preview != null && preview.IsPlaying;
        }

        public override void OnInspectorGUI() {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            DrawFields();
            if (serializedObject.ApplyModifiedProperties() | EditorGUI.EndChangeCheck()) issuesDirty = true;

            if (targets.Length > 1) return;
            // Issues are rebuilt on a layout pass, so layout and repaint draw the same boxes, and only after a change
            if (Event.current.type == EventType.Layout) {
                if (issuesDirty || EditorApplication.timeSinceStartup - lastRefreshTime > ExternalRefreshInterval) RefreshIssues();
                previewShown = preview.IsPlaying;
                eventsShown = preview.EventLog.Count;
            }
            DrawIssues();
            DrawPreview();
        }
        #endregion

        // ==================== FIELDS ====================
        #region Fields
        /// <summary> Every field in declaration order, showing only the text field that matches the source mode </summary>
        private void DrawFields() {
            bool _mixed = sourceMode.hasMultipleDifferentValues;
            bool _localized = sourceMode.enumValueIndex == (int)TextSourceMode.Localized;

            SerializedProperty _property = serializedObject.GetIterator();
            bool _enterChildren = true;
            while (_property.NextVisible(_enterChildren)) {
                _enterChildren = false;
                switch (_property.name) {
                    case ScriptField:
                        using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(_property);
                        continue;
                    case PlainTextField:
                        if (_mixed || !_localized) EditorGUILayout.PropertyField(_property, true);
                        continue;
                    case LocalizedTextField:
                        if (_mixed || _localized) EditorGUILayout.PropertyField(_property, true);
                        continue;
                }

                EditorGUILayout.PropertyField(_property, true);
#if !HARRYTOOLS_TEXTEFFECTS_LOCALIZATION
                if (_property.name == SourceModeField && !_mixed && _localized) {
                    EditorGUILayout.HelpBox("The Localization package (com.unity.localization) is not installed, so a localized source has no text.", MessageType.Warning);
                }
#endif
            }
        }
        #endregion

        // ==================== VALIDATION ====================
        #region Validation
        private void MarkIssuesDirty() {
            issuesDirty = true;
        }

        private void RefreshIssues() {
            issuesDirty = false;
            lastRefreshTime = EditorApplication.timeSinceStartup;
            EffectText _effect = (EffectText)target;
            TextEffectsSettings _settings = TextEffectsSettingsLocator.Find();
            settingsMissing = _settings == null;
            Func<string, bool> _eventExists = _effect.Events != null ? _effect.Events.Contains : (Func<string, bool>)null;

            textIssues.Clear();
            localeIssues.Clear();
            localeCodes.Clear();
            localizedMissing = false;
            if (_effect.SourceMode == TextSourceMode.Localized) {
#if HARRYTOOLS_TEXTEFFECTS_LOCALIZATION
                string _baseLocale = LocalizedTextSource.ReadAll(_effect.LocalizedText, textByLocale);
                localizedMissing = textByLocale.Count == 0;
                TextValidator.ValidateLocales(_baseLocale, textByLocale, _settings, localeIssues, _eventExists);
                localeCodes.AddRange(textByLocale.Keys);
                localeCodes.Sort(StringComparer.Ordinal);
                if (selectedLocale == null || !localeCodes.Contains(selectedLocale)) selectedLocale = _baseLocale;
#endif
                return;
            }

            if (!string.IsNullOrEmpty(_effect.PlainText)) TextValidator.ValidateText(_effect.PlainText, _settings, _eventExists, textIssues);
        }

        private void DrawIssues() {
            if (settingsMissing) EditorGUILayout.HelpBox("Text Effects settings not found: every preset and voice is reported as unknown.", MessageType.Info);
            if (localizedMissing) EditorGUILayout.HelpBox("No localized string selected, or its table entry was not found.", MessageType.Warning);

            for (int _i = 0; _i < textIssues.Count; _i++) {
                TextIssue _issue = textIssues[_i];
                EditorGUILayout.HelpBox(Describe(_issue), TypeOf(_issue));
            }

            for (int _i = 0; _i < localeIssues.Count; _i++) {
                LocaleIssue _issue = localeIssues[_i];
                EditorGUILayout.HelpBox("(" + _issue.Locale + ") " + Describe(_issue.Issue), TypeOf(_issue.Issue));
            }
        }

        private static string Describe(TextIssue _issue) {
            return _issue.Position >= 0 ? "At position " + _issue.Position + ": " + _issue.Message : _issue.Message;
        }

        private static MessageType TypeOf(TextIssue _issue) {
            return _issue.Severity == IssueSeverity.Error ? MessageType.Error : MessageType.Warning;
        }
        #endregion

        // ==================== PREVIEW ====================
        #region Preview
        private void DrawPreview() {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

            if (EditorApplication.isPlayingOrWillChangePlaymode) {
                EditorGUILayout.HelpBox("The preview is only available in edit mode.", MessageType.None);
                return;
            }

            EffectText _effect = (EffectText)target;
            if (!_effect.isActiveAndEnabled) {
                EditorGUILayout.HelpBox("The preview needs an active and enabled object.", MessageType.None);
                return;
            }

            DrawLocaleSelector(_effect);
            DrawPreviewButtons();
            DrawEventLog();
        }

        private void DrawLocaleSelector(EffectText _effect) {
            if (_effect.SourceMode != TextSourceMode.Localized || localeCodes.Count == 0) return;

            int _current = Mathf.Max(0, localeCodes.IndexOf(selectedLocale));
            int _index = EditorGUILayout.Popup("Locale", _current, localeCodes.ToArray());
            selectedLocale = localeCodes[_index];
            // Switching locale while playing starts again in the new one
            if (_index != _current && preview.IsPlaying) preview.Play(selectedLocale);
        }

        private void DrawPreviewButtons() {
            using (new EditorGUILayout.HorizontalScope()) {
                using (new EditorGUI.DisabledScope(previewShown)) {
                    if (GUILayout.Button("Play")) preview.Play(selectedLocale);
                }

                using (new EditorGUI.DisabledScope(!previewShown)) {
                    if (GUILayout.Button("Skip")) preview.Skip();
                    if (GUILayout.Button("Continue")) preview.Continue();
                    if (GUILayout.Button("Hide")) preview.Hide();
                    if (GUILayout.Button("Restart")) preview.Restart();
                    if (GUILayout.Button("Stop")) preview.Stop();
                }
            }
        }

        private void DrawEventLog() {
            if (!previewShown) return;
            if (eventsShown == 0) {
                EditorGUILayout.LabelField("No events reached yet.", EditorStyles.miniLabel);
                return;
            }

            EditorGUILayout.LabelField("Events reached", EditorStyles.miniBoldLabel);
            eventScroll = EditorGUILayout.BeginScrollView(eventScroll, GUILayout.MaxHeight(100f));
            int _count = Mathf.Min(eventsShown, preview.EventLog.Count);
            for (int _i = 0; _i < _count; _i++) EditorGUILayout.LabelField((_i + 1) + ". " + preview.EventLog[_i]);
            EditorGUILayout.EndScrollView();
        }
        #endregion
    }
}
