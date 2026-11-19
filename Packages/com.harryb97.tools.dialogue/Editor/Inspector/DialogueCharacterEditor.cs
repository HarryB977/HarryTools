using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Inspector for characters: the name in the source locale, its translations with the outdated marks and the review buttons, and the voice. A name kept in another table is shown as it is with a button that moves it to the characters table. Read-only in play mode </summary>
    [CustomEditor(typeof(DialogueCharacter))]
    internal sealed class DialogueCharacterEditor : UnityEditor.Editor {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string MoveLabel = "Move to dialogue characters table";
        private const string MarkAllReviewedLabel = "Mark all as reviewed";
        private const string NameLabel = "Name";
        private const string TranslationsLabel = "Translations";

        private SerializedProperty displayName;
        private SerializedProperty typingVoice;
        #endregion

        // ==================== UNITY ====================
        #region Unity
        private void OnEnable() {
            displayName = serializedObject.FindProperty("displayName");
            typingVoice = serializedObject.FindProperty("typingVoice");
        }

        public override void OnInspectorGUI() {
            serializedObject.Update();

            using (new EditorGUI.DisabledScope(EditorApplication.isPlaying)) {
                if (serializedObject.isEditingMultipleObjects) {
                    EditorGUILayout.PropertyField(displayName);
                } else {
                    DialogueCharacter _character = (DialogueCharacter) target;
                    if (_character.DisplayName.IsEmpty || DialogueCharacterTables.IsOwnCollection(_character)) DrawNames(_character);
                    else DrawOtherTable(_character);
                }
                EditorGUILayout.PropertyField(typingVoice);
            }

            serializedObject.ApplyModifiedProperties();
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        private void DrawOtherTable(DialogueCharacter _character) {
            EditorGUILayout.PropertyField(displayName);
            if (GUILayout.Button(MoveLabel)) {
                serializedObject.ApplyModifiedProperties();
                DialogueCharacterTables.MoveToOwnCollection(_character);
                serializedObject.Update();
            }
        }

        private void DrawNames(DialogueCharacter _character) {
            Locale _source = DialogueTables.SourceLocale();
            if (_source == null) {
                EditorGUILayout.HelpBox("The project has no source locale.", MessageType.Info);
                return;
            }

            // Assigning a missing id saves the asset, which does not happen in play mode
            string _id = EditorApplication.isPlaying ? _character.CharacterId : DialogueCharacterTables.EnsureId(_character);
            StringTableCollection _collection = DialogueCharacterTables.Find();
            string _current = DialogueTables.GetSourceText(_character.DisplayName) ?? "";

            // Delayed fields write once when editing ends, not on every keystroke
            string _typed = EditorGUILayout.DelayedTextField(new GUIContent(NameLabel, "The name in " + LocaleName(_source) + ". Written to the characters table"), _current);
            if (_typed != _current) DialogueCharacterTables.SetSourceName(_character, _typed);

            IReadOnlyList<Locale> _locales = LocalizationEditorSettings.GetLocales();
            bool _any = false;
            bool _anyOutdated = false;
            for (int _i = 0; _i < _locales.Count; _i++) {
                if (_locales[_i] == null || _locales[_i].Identifier == _source.Identifier) continue;
                if (!_any) EditorGUILayout.LabelField(TranslationsLabel, EditorStyles.boldLabel);
                _any = true;
                _anyOutdated |= DrawTranslation(_character, _collection, _id, _locales[_i]);
            }

            if (_anyOutdated && GUILayout.Button(MarkAllReviewedLabel)) DialogueTranslations.MarkReviewed(_collection, _id);
        }

        /// <summary> The translation field of one locale with its outdated note. True when it is outdated </summary>
        private static bool DrawTranslation(DialogueCharacter _character, StringTableCollection _collection, string _id, Locale _locale) {
            LocaleIdentifier _identifier = _locale.Identifier;
            string _text = DialogueTranslations.TextOf(_collection, _id, _identifier);
            string _typed = EditorGUILayout.DelayedTextField(new GUIContent(LocaleName(_locale), "The name in " + LocaleName(_locale) + ". Written to its table"), _text);
            if (_typed != _text) DialogueCharacterTables.SetName(_character, _identifier, _typed);

            if (DialogueTranslations.State(_collection, _id, _identifier) != TranslationState.Outdated) return false;
            string _previous = DialogueTranslations.PreviousSource(_collection, _id, _identifier);
            EditorGUILayout.HelpBox("Outdated. " + DialogueTranslations.PreviousSourcePrefix + (string.IsNullOrEmpty(_previous) ? "(empty)" : _previous), MessageType.Warning);
            if (GUILayout.Button(DialogueTranslations.MarkReviewedLabel)) DialogueTranslations.MarkReviewed(_collection, _id, _identifier);
            return true;
        }

        private static string LocaleName(Locale _locale) => string.IsNullOrEmpty(_locale.LocaleName) ? _locale.Identifier.Code : _locale.LocaleName;
        #endregion
    }
}
