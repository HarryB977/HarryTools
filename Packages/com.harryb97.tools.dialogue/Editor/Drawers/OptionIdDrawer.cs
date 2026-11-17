using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Lists the options of the dialogue held by the sibling field, labelled by choice node and option text. Stores the option id </summary>
    [CustomPropertyDrawer(typeof(OptionIdAttribute))]
    internal sealed class OptionIdDrawer : PropertyDrawer {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const float Spacing = 2f;
        private const float HelpMargin = 2f;
        private const string NoneLabel = "None";
        private const int ShortIdLength = 6;
        #endregion

        // ==================== DRAWER ====================
        #region Drawer
        public override float GetPropertyHeight(SerializedProperty _property, GUIContent _label) {
            float _height = EditorGUIUtility.singleLineHeight;
            if (WarningFor(_property) != null) _height += Spacing + EditorGUIUtility.singleLineHeight * 2f + HelpMargin;
            return _height;
        }

        public override void OnGUI(Rect _position, SerializedProperty _property, GUIContent _label) {
            if (_property.propertyType != SerializedPropertyType.String) {
                EditorGUI.LabelField(_position, _label.text, "Use [OptionId] on a string");
                return;
            }

            DialogueAsset _dialogue = FindDialogue(_property, ((OptionIdAttribute) attribute).DialogueField);
            string _id = _property.stringValue;
            List<Entry> _entries = Collect(_dialogue);

            Rect _line = new Rect(_position.x, _position.y, _position.width, EditorGUIUtility.singleLineHeight);
            EditorGUI.BeginProperty(_line, _label, _property);
            Rect _field = EditorGUI.PrefixLabel(_line, _label);
            int _indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            using (new EditorGUI.DisabledScope(_dialogue == null || _property.hasMultipleDifferentValues)) {
                if (GUI.Button(_field, DisplayText(_entries, _id, _property.hasMultipleDifferentValues), EditorStyles.popup)) ShowMenu(_property, _entries, _id);
            }

            EditorGUI.indentLevel = _indent;
            EditorGUI.EndProperty();

            string _warning = WarningFor(_property);
            if (_warning != null) {
                Rect _help = new Rect(_position.x, _line.yMax + Spacing, _position.width, _position.height - _line.height - Spacing);
                EditorGUI.HelpBox(_help, _warning, MessageType.Warning);
            }
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        private struct Entry {
            public string Id;
            public string Label;
        }

        private string WarningFor(SerializedProperty _property) {
            if (_property.propertyType != SerializedPropertyType.String || _property.hasMultipleDifferentValues) return null;
            DialogueAsset _dialogue = FindDialogue(_property, ((OptionIdAttribute) attribute).DialogueField);
            if (_dialogue == null) return null;

            string _id = _property.stringValue;
            if (string.IsNullOrEmpty(_id)) return "No option selected.";
            List<Entry> _entries = Collect(_dialogue);
            for (int _i = 0; _i < _entries.Count; _i++) {
                if (_entries[_i].Id == _id) return null;
            }
            return "The option '" + _id + "' does not exist in this dialogue.";
        }

        private static DialogueAsset FindDialogue(SerializedProperty _property, string _dialogueField) {
            string _path = _property.propertyPath;
            int _dot = _path.LastIndexOf('.');
            string _siblingPath = (_dot >= 0 ? _path.Substring(0, _dot + 1) : string.Empty) + _dialogueField;
            SerializedProperty _sibling = _property.serializedObject.FindProperty(_siblingPath);
            if (_sibling == null || _sibling.propertyType != SerializedPropertyType.ObjectReference || _sibling.hasMultipleDifferentValues) return null;
            return _sibling.objectReferenceValue as DialogueAsset;
        }

        private static List<Entry> Collect(DialogueAsset _dialogue) {
            List<Entry> _entries = new List<Entry>();
            if (_dialogue == null) return _entries;

            int _choiceIndex = 0;
            for (int _i = 0; _i < _dialogue.Nodes.Count; _i++) {
                if (!(_dialogue.Nodes[_i] is ChoiceNode _choice)) continue;
                _choiceIndex++;
                for (int _o = 0; _o < _choice.Options.Count; _o++) {
                    ChoiceOption _option = _choice.Options[_o];
                    if (_option == null || string.IsNullOrEmpty(_option.Id)) continue;
                    string _text = SourceText(_option.Text);
                    if (string.IsNullOrEmpty(_text)) _text = ShortId(_option.Id);
                    _entries.Add(new Entry { Id = _option.Id, Label = "Choice " + _choiceIndex + "/" + _text });
                }
            }
            return _entries;
        }

        /// <summary> The text from the first table of the collection that has one. Null when it cannot be resolved </summary>
        private static string SourceText(LocalizedString _string) {
            if (_string == null || _string.IsEmpty) return null;
            StringTableCollection _collection = LocalizationEditorSettings.GetStringTableCollection(_string.TableReference);
            if (_collection == null || _collection.SharedData == null) return null;
            SharedTableData.SharedTableEntry _entry = _collection.SharedData.GetEntryFromReference(_string.TableEntryReference);
            if (_entry == null) return null;

            IReadOnlyList<StringTable> _tables = _collection.StringTables;
            for (int _i = 0; _i < _tables.Count; _i++) {
                string _value = _tables[_i] != null ? _tables[_i].GetEntry(_entry.Id)?.Value : null;
                if (!string.IsNullOrEmpty(_value)) return _value;
            }
            return null;
        }

        private static string ShortId(string _id) {
            return _id.Length <= ShortIdLength ? _id : _id.Substring(0, ShortIdLength);
        }

        private static string DisplayText(List<Entry> _entries, string _id, bool _mixed) {
            if (_mixed) return "—";
            if (string.IsNullOrEmpty(_id)) return NoneLabel;
            for (int _i = 0; _i < _entries.Count; _i++) {
                if (_entries[_i].Id == _id) return _entries[_i].Label;
            }
            return _id + " (missing)";
        }

        private static void ShowMenu(SerializedProperty _property, List<Entry> _entries, string _current) {
            UnityEngine.Object[] _targets = _property.serializedObject.targetObjects;
            string _path = _property.propertyPath;

            GenericMenu _menu = new GenericMenu();
            _menu.AddItem(new GUIContent(NoneLabel), string.IsNullOrEmpty(_current), () => Assign(_targets, _path, string.Empty));
            for (int _i = 0; _i < _entries.Count; _i++) {
                string _id = _entries[_i].Id;
                // Option labels hold '/', which the menu would read as submenus
                string _label = _entries[_i].Label.Replace('/', '∕');
                _menu.AddItem(new GUIContent(_label), _id == _current, () => Assign(_targets, _path, _id));
            }
            _menu.ShowAsContext();
        }

        // The menu answers after the drawer call, so it edits through a fresh SerializedObject (keeps Undo)
        private static void Assign(UnityEngine.Object[] _targets, string _path, string _id) {
            using (SerializedObject _object = new SerializedObject(_targets)) {
                SerializedProperty _property = _object.FindProperty(_path);
                if (_property == null) return;
                _property.stringValue = _id;
                _object.ApplyModifiedProperties();
            }
        }
        #endregion
    }
}
