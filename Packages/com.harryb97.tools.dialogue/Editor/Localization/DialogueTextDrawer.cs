using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Edits a dialogue text in the source locale and stores it in the dialogue's string table. The other locales are shown read only </summary>
    [CustomPropertyDrawer(typeof(DialogueTextAttribute))]
    internal sealed class DialogueTextDrawer : PropertyDrawer {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const float Spacing = 2f;
        private const int MinLines = 2;
        private const int MaxLines = 8;
        private const int HelpLines = 2;
        private const string Untranslated = "untranslated";
        private const string NoLocaleWarning = "No source locale. Add locales in the Localization settings, or check the source locale in the dialogue settings, to write text.";
        #endregion

        // ==================== DRAWER ====================
        #region Drawer
        public override float GetPropertyHeight(SerializedProperty _property, GUIContent _label) {
            float _line = EditorGUIUtility.singleLineHeight;
            if (!IsLocalizedString(_property) || _property.serializedObject.isEditingMultipleObjects) return _line;

            Locale _source = DialogueTables.SourceLocale();
            if (_source == null) return _line + Spacing + _line * HelpLines;

            string _text = SourceText(_property, _source) ?? "";
            int _lines = Mathf.Clamp(_text.Split('\n').Length, MinLines, MaxLines);
            int _others = LocalizationEditorSettings.GetLocales().Count - 1;
            return _line + Spacing + _line * _lines + Mathf.Max(0, _others) * (_line + Spacing);
        }

        public override void OnGUI(Rect _position, SerializedProperty _property, GUIContent _label) {
            float _line = EditorGUIUtility.singleLineHeight;
            Rect _row = new Rect(_position.x, _position.y, _position.width, _line);

            if (!IsLocalizedString(_property)) {
                EditorGUI.LabelField(_row, _label.text, "Use [DialogueText] on a LocalizedString");
                return;
            }
            if (_property.serializedObject.isEditingMultipleObjects) {
                EditorGUI.LabelField(_row, _label.text, "Edit one dialogue at a time");
                return;
            }

            EditorGUI.LabelField(_row, _label);
            Locale _source = DialogueTables.SourceLocale();
            if (_source == null) {
                Rect _help = new Rect(_position.x, _row.yMax + Spacing, _position.width, _line * HelpLines);
                EditorGUI.HelpBox(EditorGUI.IndentedRect(_help), NoLocaleWarning, MessageType.Warning);
                return;
            }

            DialogueAsset _asset = _property.serializedObject.targetObject as DialogueAsset;
            string _text = SourceText(_property, _source) ?? "";
            int _lines = Mathf.Clamp(_text.Split('\n').Length, MinLines, MaxLines);
            Rect _area = EditorGUI.IndentedRect(new Rect(_position.x, _row.yMax + Spacing, _position.width, _line * _lines));

            using (new EditorGUI.DisabledScope(_asset == null)) {
                EditorGUI.BeginChangeCheck();
                string _edited = EditorGUI.TextArea(_area, _text, EditorStyles.textArea);
                if (EditorGUI.EndChangeCheck()) Write(_property, _asset, _edited);
            }

            DrawOtherLocales(_property, _source, _position, _area.yMax);
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        private void Write(SerializedProperty _property, DialogueAsset _asset, string _text) {
            string _key = DialogueTables.KeyFor(_property, ((DialogueTextAttribute) attribute).KeySuffix);
            if (_key == null) return;

            int _group = Undo.GetCurrentGroup();
            // Pending edits go first so the direct writes below do not get overwritten
            _property.serializedObject.ApplyModifiedProperties();
            if (!DialogueTables.WriteSourceText(_asset, _key, _text, out Guid _collection, out long _keyId)) return;

            _property.serializedObject.Update();
            DialogueTables.WriteReference(_property, _collection, _keyId);
            _property.serializedObject.ApplyModifiedProperties();
            Undo.CollapseUndoOperations(_group);
            DialogueTables.NotifySourceTextWritten(_asset);
        }

        private static void DrawOtherLocales(SerializedProperty _property, Locale _source, Rect _position, float _top) {
            DialogueTables.ReadReference(_property, out TableReference _table, out TableEntryReference _entry);
            IReadOnlyList<Locale> _locales = LocalizationEditorSettings.GetLocales();
            float _line = EditorGUIUtility.singleLineHeight;
            float _y = _top + Spacing;

            using (new EditorGUI.DisabledScope(true)) {
                for (int _i = 0; _i < _locales.Count; _i++) {
                    if (_locales[_i] == null || _locales[_i].Identifier == _source.Identifier) continue;
                    string _value = DialogueTables.GetText(_table, _entry, _locales[_i].Identifier);
                    Rect _row = EditorGUI.IndentedRect(new Rect(_position.x, _y, _position.width, _line));
                    EditorGUI.LabelField(_row, _locales[_i].Identifier.Code, string.IsNullOrEmpty(_value) ? Untranslated : FirstLine(_value));
                    _y += _line + Spacing;
                }
            }
        }

        private static string SourceText(SerializedProperty _property, Locale _source) {
            DialogueTables.ReadReference(_property, out TableReference _table, out TableEntryReference _entry);
            return DialogueTables.GetText(_table, _entry, _source.Identifier);
        }

        private static bool IsLocalizedString(SerializedProperty _property) {
            return _property.propertyType == SerializedPropertyType.Generic && _property.FindPropertyRelative("m_TableReference") != null && _property.FindPropertyRelative("m_TableEntryReference") != null;
        }

        private static string FirstLine(string _value) {
            int _break = _value.IndexOf('\n');
            return _break < 0 ? _value : _value.Substring(0, _break) + " …";
        }
        #endregion
    }
}
