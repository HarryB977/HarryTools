using System;
using UnityEditor;
using UnityEngine;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Inspector for dialogues: the asset settings and a foldout per node, with add and remove </summary>
    [CustomEditor(typeof(DialogueAsset))]
    internal sealed class DialogueAssetEditor : UnityEditor.Editor {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const int ShortIdLength = 6;
        private const int MaxSummaryLength = 40;
        private const float RemoveWidth = 64f;
        private const string NodeSuffix = "Node";

        private static readonly (string Label, Func<DialogueAsset, DialogueNode> Add)[] NodeTypes = {
            ("Line", _asset => _asset.AddNode<LineNode>()),
            ("Choice", _asset => _asset.AddNode<ChoiceNode>()),
            ("Branch", _asset => _asset.AddNode<BranchNode>()),
            ("Actions", _asset => _asset.AddNode<ActionsNode>()),
            ("Jump", _asset => _asset.AddNode<JumpNode>()),
            ("End", _asset => _asset.AddNode<EndNode>())
        };

        private SerializedProperty overrideTime;
        private SerializedProperty time;
        private SerializedProperty overrideAmbientPolicy;
        private SerializedProperty ambientPolicy;
        private SerializedProperty allowSkip;
        private SerializedProperty nodes;
        #endregion

        // ==================== UNITY ====================
        #region Unity
        private void OnEnable() {
            overrideTime = serializedObject.FindProperty("overrideTime");
            time = serializedObject.FindProperty("time");
            overrideAmbientPolicy = serializedObject.FindProperty("overrideAmbientPolicy");
            ambientPolicy = serializedObject.FindProperty("ambientPolicy");
            allowSkip = serializedObject.FindProperty("allowSkip");
            nodes = serializedObject.FindProperty("nodes");
        }

        public override void OnInspectorGUI() {
            serializedObject.Update();
            DialogueAsset _asset = (DialogueAsset) target;

            DrawSettings();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Nodes", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(serializedObject.isEditingMultipleObjects)) {
                if (serializedObject.isEditingMultipleObjects) EditorGUILayout.HelpBox("Edit one dialogue at a time to change its nodes.", MessageType.Info);
                else DrawNodes(_asset);

                if (GUILayout.Button("Add node")) ShowAddMenu(_asset);
            }

            serializedObject.ApplyModifiedProperties();
        }
        #endregion

        // ==================== LABELS ====================
        #region Labels
        /// <summary> The node type without the "Node" suffix </summary>
        internal static string TypeName(DialogueNode _node) {
            if (_node == null) return "Missing";
            string _name = _node.GetType().Name;
            return _name.EndsWith(NodeSuffix) && _name.Length > NodeSuffix.Length ? _name.Substring(0, _name.Length - NodeSuffix.Length) : _name;
        }

        internal static string ShortId(string _id) {
            if (string.IsNullOrEmpty(_id)) return "";
            return _id.Length <= ShortIdLength ? _id : _id.Substring(0, ShortIdLength);
        }

        /// <summary> First line of the node's text in the source locale, shortened. Empty when it has none </summary>
        internal static string Summary(DialogueNode _node) {
            string _text = null;
            switch (_node) {
                case LineNode _line:
                    _text = DialogueTables.GetSourceText(_line.Text);
                    break;
                case ChoiceNode _choice:
                    if (_choice.Options.Count > 0 && _choice.Options[0] != null) _text = DialogueTables.GetSourceText(_choice.Options[0].Text);
                    break;
                case JumpNode _jump:
                    _text = _jump.Target != null ? _jump.Target.name : null;
                    break;
            }
            if (string.IsNullOrEmpty(_text)) return "";

            int _break = _text.IndexOf('\n');
            if (_break >= 0) _text = _text.Substring(0, _break);
            return _text.Length <= MaxSummaryLength ? _text : _text.Substring(0, MaxSummaryLength) + "…";
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        private void DrawSettings() {
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(overrideTime);
            using (new EditorGUI.DisabledScope(!overrideTime.boolValue && !overrideTime.hasMultipleDifferentValues)) {
                EditorGUILayout.PropertyField(time);
            }
            EditorGUILayout.PropertyField(overrideAmbientPolicy);
            using (new EditorGUI.DisabledScope(!overrideAmbientPolicy.boolValue && !overrideAmbientPolicy.hasMultipleDifferentValues)) {
                EditorGUILayout.PropertyField(ambientPolicy);
            }
            EditorGUILayout.PropertyField(allowSkip);
        }

        private void DrawNodes(DialogueAsset _asset) {
            for (int _i = 0; _i < nodes.arraySize; _i++) {
                SerializedProperty _element = nodes.GetArrayElementAtIndex(_i);
                DialogueNode _node = _element.managedReferenceValue as DialogueNode;

                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
                    using (new EditorGUILayout.HorizontalScope()) {
                        _element.isExpanded = EditorGUILayout.Foldout(_element.isExpanded, Header(_node), true);
                        using (new EditorGUI.DisabledScope(_node is StartNode)) {
                            if (GUILayout.Button("Remove", GUILayout.Width(RemoveWidth))) {
                                Remove(_asset, _i, _node);
                                GUIUtility.ExitGUI();
                            }
                        }
                    }

                    if (_element.isExpanded && _node != null) {
                        EditorGUI.indentLevel++;
                        DrawNodeFields(_element);
                        EditorGUI.indentLevel--;
                    }
                }
            }
        }

        private static string Header(DialogueNode _node) {
            if (_node == null) return "Missing node (its type no longer exists)";
            string _summary = Summary(_node);
            string _header = TypeName(_node) + " · " + ShortId(_node.Id);
            return string.IsNullOrEmpty(_summary) ? _header : _header + " · " + _summary;
        }

        private static void DrawNodeFields(SerializedProperty _element) {
            SerializedProperty _child = _element.Copy();
            SerializedProperty _end = _element.GetEndProperty();
            bool _enter = true;
            while (_child.NextVisible(_enter) && !SerializedProperty.EqualContents(_child, _end)) {
                _enter = false;
                // Ids and graph positions are managed by the tool
                if (_child.name == "id" || _child.name == "position") continue;
                EditorGUILayout.PropertyField(_child, true);
            }
        }

        private void Remove(DialogueAsset _asset, int _index, DialogueNode _node) {
            serializedObject.ApplyModifiedProperties();
            Undo.RecordObject(_asset, "Remove Dialogue Node");
            if (_node != null) {
                _asset.RemoveNode(_node.Id);
            } else {
                // A node whose class was deleted cannot be found by id
                nodes.DeleteArrayElementAtIndex(_index);
                serializedObject.ApplyModifiedProperties();
            }
            EditorUtility.SetDirty(_asset);
            serializedObject.Update();
        }

        private static void ShowAddMenu(DialogueAsset _asset) {
            GenericMenu _menu = new GenericMenu();
            for (int _i = 0; _i < NodeTypes.Length; _i++) {
                Func<DialogueAsset, DialogueNode> _add = NodeTypes[_i].Add;
                _menu.AddItem(new GUIContent(NodeTypes[_i].Label), false, () => {
                    Undo.RecordObject(_asset, "Add Dialogue Node");
                    _add(_asset);
                    EditorUtility.SetDirty(_asset);
                });
            }
            _menu.ShowAsContext();
        }
        #endregion
    }
}
