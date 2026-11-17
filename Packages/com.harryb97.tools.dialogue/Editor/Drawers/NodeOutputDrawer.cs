using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Picks the node an output leads to from the nodes of its dialogue. Stores the target node id </summary>
    [CustomPropertyDrawer(typeof(NodeOutput))]
    internal sealed class NodeOutputDrawer : PropertyDrawer {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string TargetField = "target";
        private const string EndLabel = "None (end)";
        #endregion

        // ==================== DRAWER ====================
        #region Drawer
        public override float GetPropertyHeight(SerializedProperty _property, GUIContent _label) {
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect _position, SerializedProperty _property, GUIContent _label) {
            SerializedProperty _target = _property.FindPropertyRelative(TargetField);
            DialogueAsset _dialogue = _property.serializedObject.targetObject as DialogueAsset;
            if (_dialogue == null || _target == null) {
                if (_target != null) EditorGUI.PropertyField(_position, _target, _label);
                else EditorGUI.LabelField(_position, _label.text, "Missing target");
                return;
            }

            EditorGUI.BeginProperty(_position, _label, _target);
            Rect _field = EditorGUI.PrefixLabel(_position, _label);
            int _indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            List<Entry> _entries = Collect(_dialogue);
            bool _mixed = _target.hasMultipleDifferentValues || _property.serializedObject.isEditingMultipleObjects;
            using (new EditorGUI.DisabledScope(_mixed)) {
                if (GUI.Button(_field, _mixed ? "—" : DisplayText(_entries, _target.stringValue), EditorStyles.popup)) {
                    ShowMenu(_property.serializedObject.targetObject, _target.propertyPath, _entries, _target.stringValue);
                }
            }

            EditorGUI.indentLevel = _indent;
            EditorGUI.EndProperty();
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        private struct Entry {
            public string Id;
            public string Label;
        }

        /// <summary> Every node but the start node, labelled "Type · text"; the short id is added when labels repeat </summary>
        private static List<Entry> Collect(DialogueAsset _dialogue) {
            List<Entry> _entries = new List<Entry>();
            Dictionary<string, int> _uses = new Dictionary<string, int>();

            for (int _i = 0; _i < _dialogue.Nodes.Count; _i++) {
                DialogueNode _node = _dialogue.Nodes[_i];
                if (_node == null || _node is StartNode || string.IsNullOrEmpty(_node.Id)) continue;
                string _summary = DialogueAssetEditor.Summary(_node);
                string _label = DialogueAssetEditor.TypeName(_node) + (string.IsNullOrEmpty(_summary) ? "" : " · " + _summary);
                _uses[_label] = _uses.TryGetValue(_label, out int _count) ? _count + 1 : 1;
                _entries.Add(new Entry { Id = _node.Id, Label = _label });
            }

            for (int _i = 0; _i < _entries.Count; _i++) {
                if (_uses[_entries[_i].Label] > 1) _entries[_i] = new Entry { Id = _entries[_i].Id, Label = _entries[_i].Label + " · " + DialogueAssetEditor.ShortId(_entries[_i].Id) };
            }
            return _entries;
        }

        private static string DisplayText(List<Entry> _entries, string _id) {
            if (string.IsNullOrEmpty(_id)) return EndLabel;
            for (int _i = 0; _i < _entries.Count; _i++) {
                if (_entries[_i].Id == _id) return _entries[_i].Label;
            }
            return _id + " (missing)";
        }

        private static void ShowMenu(Object _owner, string _path, List<Entry> _entries, string _current) {
            GenericMenu _menu = new GenericMenu();
            _menu.AddItem(new GUIContent(EndLabel), string.IsNullOrEmpty(_current), () => Assign(_owner, _path, string.Empty));
            for (int _i = 0; _i < _entries.Count; _i++) {
                string _id = _entries[_i].Id;
                // Node text may hold '/', which the menu would read as submenus
                _menu.AddItem(new GUIContent(_entries[_i].Label.Replace('/', '∕')), _id == _current, () => Assign(_owner, _path, _id));
            }
            _menu.ShowAsContext();
        }

        // The menu answers after the drawer call, so it edits through a fresh SerializedObject (keeps Undo)
        private static void Assign(Object _owner, string _path, string _id) {
            if (_owner == null) return;
            using (SerializedObject _object = new SerializedObject(_owner)) {
                SerializedProperty _property = _object.FindProperty(_path);
                if (_property == null) return;
                _property.stringValue = _id;
                _object.ApplyModifiedProperties();
            }
        }
        #endregion
    }
}
