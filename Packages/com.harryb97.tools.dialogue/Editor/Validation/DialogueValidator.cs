using System;
using System.Collections.Generic;
using HarryTools.TextEffects;
using HarryTools.TextEffects.Editor;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.SceneManagement;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Finds problems in a dialogue asset. It only reports; nothing here blocks play or builds </summary>
    internal static class DialogueValidator {
        // ==================== VARIABLES ===================
        #region Private Variables
        private static readonly List<NodeOutput> outputBuffer = new List<NodeOutput>();
        private static readonly List<TextIssue> textIssueBuffer = new List<TextIssue>();
        #endregion

        // ==================== METHODS ====================
        #region Public Methods
        /// <summary> Adds every problem of the dialogue. A null '_launchedAsAmbient' means no dialogue is known to start as ambient </summary>
        internal static void Validate(DialogueAsset _asset, ISet<DialogueAsset> _launchedAsAmbient, List<DialogueIssue> _into) {
            if (_asset == null || _into == null) return;

            Dictionary<string, DialogueNode> _byId = new Dictionary<string, DialogueNode>();
            StartNode _start = null;
            for (int _i = 0; _i < _asset.NodeList.Count; _i++) {
                DialogueNode _node = _asset.NodeList[_i];
                if (_node == null) {
                    Add(_into, DialogueIssueSeverity.Warning, "", "A node has a missing type and was skipped.");
                    continue;
                }
                if (_start == null && _node is StartNode _found) _start = _found;
                if (!string.IsNullOrEmpty(_node.Id)) _byId[_node.Id] = _node;
            }

            CheckReachability(_asset, _start, _byId, _into);
            bool _ambient = _launchedAsAmbient != null && _launchedAsAmbient.Contains(_asset);

            for (int _i = 0; _i < _asset.NodeList.Count; _i++) {
                DialogueNode _node = _asset.NodeList[_i];
                if (_node == null) continue;
                CheckOutputs(_node, _node == _start, _byId, _into);

                switch (_node) {
                    case LineNode _line:
                        CheckText(_line.Text, _line.Id, "Line", true, _line.Events != null ? (Func<string, bool>)_line.Events.Contains : null, _into);
                        break;
                    case ChoiceNode _choice:
                        CheckChoice(_choice, _ambient, _into);
                        break;
                    case JumpNode _jump:
                        if (_jump.Target == null) Add(_into, DialogueIssueSeverity.Warning, _jump.Id, "Jump has no target.");
                        break;
                }
            }
        }

        /// <summary> Dialogues started as ambient by a launcher in an open scene or in a prefab of the project. Jumps, actions and code are not followed </summary>
        internal static HashSet<DialogueAsset> FindAmbientLaunches() {
            HashSet<DialogueAsset> _found = new HashSet<DialogueAsset>();

            for (int _s = 0; _s < SceneManager.sceneCount; _s++) {
                Scene _scene = SceneManager.GetSceneAt(_s);
                if (!_scene.isLoaded) continue;
                GameObject[] _roots = _scene.GetRootGameObjects();
                for (int _r = 0; _r < _roots.Length; _r++) AddAmbient(_roots[_r], _found);
            }

            string[] _guids = AssetDatabase.FindAssets("t:Prefab");
            for (int _i = 0; _i < _guids.Length; _i++) {
                GameObject _prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(_guids[_i]));
                if (_prefab != null) AddAmbient(_prefab, _found);
            }
            return _found;
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        private static void AddAmbient(GameObject _root, HashSet<DialogueAsset> _into) {
            DialogueLauncher[] _launchers = _root.GetComponentsInChildren<DialogueLauncher>(true);
            for (int _i = 0; _i < _launchers.Length; _i++) {
                if (!_launchers[_i].IsMain && _launchers[_i].Dialogue != null) _into.Add(_launchers[_i].Dialogue);
            }
        }

        private static void Add(List<DialogueIssue> _into, DialogueIssueSeverity _severity, string _nodeId, string _message) {
            _into.Add(new DialogueIssue(_severity, _nodeId, _message));
        }

        private static void CheckReachability(DialogueAsset _asset, StartNode _start, Dictionary<string, DialogueNode> _byId, List<DialogueIssue> _into) {
            if (_start == null) {
                Add(_into, DialogueIssueSeverity.Warning, "", "The dialogue has no Start node.");
                return;
            }
            if (!_start.Next.IsConnected) Add(_into, DialogueIssueSeverity.Warning, _start.Id, "Start is not connected.");

            HashSet<DialogueNode> _reached = new HashSet<DialogueNode> { _start };
            Stack<DialogueNode> _pending = new Stack<DialogueNode>();
            _pending.Push(_start);
            List<NodeOutput> _outputs = new List<NodeOutput>();
            while (_pending.Count > 0) {
                _outputs.Clear();
                _pending.Pop().CollectOutputs(_outputs);
                for (int _o = 0; _o < _outputs.Count; _o++) {
                    NodeOutput _output = _outputs[_o];
                    if (_output == null || !_output.IsConnected || !_byId.TryGetValue(_output.Target, out DialogueNode _next)) continue;
                    if (_reached.Add(_next)) _pending.Push(_next);
                }
            }

            for (int _i = 0; _i < _asset.NodeList.Count; _i++) {
                DialogueNode _node = _asset.NodeList[_i];
                if (_node != null && !_reached.Contains(_node)) Add(_into, DialogueIssueSeverity.Warning, _node.Id, "Node is not reachable from Start.");
            }
        }

        private static void CheckOutputs(DialogueNode _node, bool _isStart, Dictionary<string, DialogueNode> _byId, List<DialogueIssue> _into) {
            ChoiceNode _choice = _node as ChoiceNode;
            outputBuffer.Clear();
            _node.CollectOutputs(outputBuffer);

            for (int _i = 0; _i < outputBuffer.Count; _i++) {
                NodeOutput _output = outputBuffer[_i];
                if (_output == null) continue;

                if (_output.IsConnected) {
                    if (!_byId.ContainsKey(_output.Target)) Add(_into, DialogueIssueSeverity.Warning, _node.Id, "Output points to a node that no longer exists.");
                    continue;
                }
                // The unconnected Start has its own warning
                if (_isStart) continue;
                if (_choice != null && _output == _choice.NoAnswer) {
                    if (_choice.TimeoutRule == TimeoutRule.NoAnswer) Add(_into, DialogueIssueSeverity.Info, _node.Id, "No Answer output is not connected.");
                    continue;
                }
                Add(_into, DialogueIssueSeverity.Info, _node.Id, "Output is not connected.");
            }
        }

        private static void CheckChoice(ChoiceNode _choice, bool _ambient, List<DialogueIssue> _into) {
            if (_ambient) Add(_into, DialogueIssueSeverity.Warning, _choice.Id, "Choice in a dialogue launched as ambient.");

            bool _hasDefault = false;
            for (int _i = 0; _i < _choice.Options.Count; _i++) {
                ChoiceOption _option = _choice.Options[_i];
                if (_option == null) continue;
                _hasDefault |= _option.IsDefault;
                CheckText(_option.Text, _choice.Id, "Option", true, null, _into);
                CheckText(_option.LockedReason, _choice.Id, "Locked reason", false, null, _into);
            }

            if (_choice.UseTimer && _choice.TimeoutRule == TimeoutRule.ChooseSelected && !_hasDefault) {
                Add(_into, DialogueIssueSeverity.Warning, _choice.Id, "Timer chooses the selected option but no option is the default.");
            }
        }

        /// <summary> Reports a missing table entry, empty text and markup problems of the source locale text. '_required' makes an empty reference a problem </summary>
        private static void CheckText(LocalizedString _text, string _nodeId, string _label, bool _required, Func<string, bool> _eventExists, List<DialogueIssue> _into) {
            if (_text == null || _text.IsEmpty) {
                if (_required) Add(_into, DialogueIssueSeverity.Warning, _nodeId, _label + " has no text.");
                return;
            }

            StringTableCollection _collection = LocalizationEditorSettings.GetStringTableCollection(_text.TableReference);
            if (_collection == null || _collection.SharedData == null || _collection.SharedData.GetEntryFromReference(_text.TableEntryReference) == null) {
                Add(_into, DialogueIssueSeverity.Warning, _nodeId, _label + " refers to a table entry that no longer exists.");
                return;
            }

            string _source = DialogueTables.GetSourceText(_text);
            if (string.IsNullOrWhiteSpace(_source)) {
                if (_required) Add(_into, DialogueIssueSeverity.Warning, _nodeId, _label + " has no text.");
                return;
            }

            textIssueBuffer.Clear();
            TextValidator.ValidateText(_source, TextEffectsSettings.Current, _eventExists, textIssueBuffer);
            for (int _i = 0; _i < textIssueBuffer.Count; _i++) {
                Add(_into, DialogueIssueSeverity.Warning, _nodeId, _label + " text: " + textIssueBuffer[_i].Message);
            }
        }
        #endregion
    }
}
