using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Every change the graph window makes to a dialogue asset. Each one records undo and marks the asset dirty </summary>
    internal static class DialogueGraphOps {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string CreateNodeUndo = "Create Dialogue Node";
        private const string ConnectUndo = "Connect";
        private const string DisconnectUndo = "Disconnect";
        private const string DeleteUndo = "Delete";
        private const string MoveUndo = "Move";
        private const string OptionUndo = "Edit Dialogue Options";
        private const string CaseUndo = "Edit Dialogue Cases";
        private const string GroupUndo = "Edit Dialogue Group";
        private const string NoteUndo = "Edit Dialogue Note";
        private const string AutoLayoutUndo = "Auto Layout";
        private const float LayoutColumnWidth = 350f;
        private const float LayoutRowHeight = 200f;

        private static readonly List<NodeOutput> outputBuffer = new List<NodeOutput>();
        #endregion

        // ==================== NODES ====================
        #region Nodes
        /// <summary> Adds a node of the type at the position. Null for the start node, which every dialogue already has, and for types that are not nodes </summary>
        internal static DialogueNode CreateNode(DialogueAsset _asset, Type _type, Vector2 _position) {
            if (_asset == null || _type == null || _type.IsAbstract || !typeof(DialogueNode).IsAssignableFrom(_type)) return null;
            if (_type == typeof(StartNode)) return null;

            Begin(_asset, CreateNodeUndo);
            DialogueNode _node = (DialogueNode)Activator.CreateInstance(_type);
            _node.Id = DialogueIds.New();
            _node.Position = _position;
            _asset.NodeList.Add(_node);
            _asset.EnsureIds();
            End(_asset);
            return _node;
        }

        /// <summary> Adds a node and points the output at it </summary>
        internal static DialogueNode CreateConnected(DialogueAsset _asset, Type _type, Vector2 _position, string _outputId) {
            NodeOutput _output = FindOutput(_asset, _outputId);
            DialogueNode _node = CreateNode(_asset, _type, _position);
            if (_node == null || _output == null) return _node;

            // CreateNode already recorded the asset in this undo group
            _output.Target = _node.Id;
            End(_asset);
            return _node;
        }

        /// <summary> Points the output at the node. An output has a single connection, so the previous one is replaced </summary>
        internal static void Connect(DialogueAsset _asset, string _outputId, string _targetNodeId) {
            NodeOutput _output = FindOutput(_asset, _outputId);
            DialogueNode _target = _asset != null ? _asset.Find(_targetNodeId) : null;
            if (_output == null || _target == null || _target is StartNode) return;

            Begin(_asset, ConnectUndo);
            _output.Target = _targetNodeId;
            End(_asset);
        }

        internal static void Disconnect(DialogueAsset _asset, string _outputId) {
            NodeOutput _output = FindOutput(_asset, _outputId);
            if (_output == null) return;

            Begin(_asset, DisconnectUndo);
            _output.Target = "";
            End(_asset);
        }

        /// <summary> Removes the nodes (never the start node), groups and notes with these ids. Outputs that pointed to a removed node are cleared </summary>
        internal static void Delete(DialogueAsset _asset, IReadOnlyCollection<string> _ids) {
            if (_asset == null || _ids == null || _ids.Count == 0) return;

            Begin(_asset, DeleteUndo);
            List<string> _keys = new List<string>();
            foreach (string _id in _ids) {
                DialogueNode _node = _asset.Find(_id);
                if (_node != null) _keys.AddRange(DialogueTables.KeysOf(_node));
                _asset.RemoveNode(_id);
                _asset.GroupList.RemoveAll(_group => _group != null && _group.Id == _id);
                _asset.NoteList.RemoveAll(_note => _note != null && _note.Id == _id);
            }
            DialogueTables.RemoveEntries(_asset, _keys);
            End(_asset);
        }

        /// <summary> Sets the position of nodes by id, and the top left corner of notes by id </summary>
        internal static void Move(DialogueAsset _asset, IReadOnlyDictionary<string, Vector2> _positions) {
            if (_asset == null || _positions == null || _positions.Count == 0) return;

            Begin(_asset, MoveUndo);
            foreach (KeyValuePair<string, Vector2> _pair in _positions) {
                DialogueNote _note = FindNote(_asset, _pair.Key);
                if (_note != null) {
                    Rect _rect = _note.Rect;
                    _rect.position = _pair.Value;
                    _note.Rect = _rect;
                    continue;
                }
                DialogueNode _node = _asset.Find(_pair.Key);
                if (_node != null) _node.Position = _pair.Value;
            }
            End(_asset);
        }
        #endregion

        // ==================== OPTIONS AND CASES ====================
        #region Options and Cases
        internal static ChoiceOption AddOption(DialogueAsset _asset, ChoiceNode _node) {
            if (_asset == null || _node == null) return null;

            Begin(_asset, OptionUndo);
            ChoiceOption _option = new ChoiceOption();
            _node.Options.Add(_option);
            _asset.EnsureIds();
            End(_asset);
            return _option;
        }

        internal static void RemoveOption(DialogueAsset _asset, ChoiceNode _node, string _optionId) {
            if (_asset == null || _node == null) return;

            int _index = _node.Options.FindIndex(_option => _option != null && _option.Id == _optionId);
            if (_index < 0) return;

            Begin(_asset, OptionUndo);
            List<string> _keys = DialogueTables.KeysOf(_node.Options[_index]);
            _node.Options.RemoveAt(_index);
            DialogueTables.RemoveEntries(_asset, _keys);
            End(_asset);
        }

        internal static void MoveOption(DialogueAsset _asset, ChoiceNode _node, int _from, int _to) {
            if (_asset == null || _node == null) return;
            MoveItem(_asset, _node.Options, _from, _to, OptionUndo);
        }

        internal static BranchCase AddCase(DialogueAsset _asset, BranchNode _node) {
            if (_asset == null || _node == null) return null;

            Begin(_asset, CaseUndo);
            BranchCase _case = new BranchCase();
            _node.Cases.Add(_case);
            _asset.EnsureIds();
            End(_asset);
            return _case;
        }

        internal static void RemoveCase(DialogueAsset _asset, BranchNode _node, int _index) {
            if (_asset == null || _node == null || _index < 0 || _index >= _node.Cases.Count) return;

            Begin(_asset, CaseUndo);
            _node.Cases.RemoveAt(_index);
            End(_asset);
        }

        internal static void MoveCase(DialogueAsset _asset, BranchNode _node, int _from, int _to) {
            if (_asset == null || _node == null) return;
            MoveItem(_asset, _node.Cases, _from, _to, CaseUndo);
        }
        #endregion

        // ==================== GROUPS AND NOTES ====================
        #region Groups and Notes
        /// <summary> Adds a group around the nodes. A node belongs to one group at most, so it leaves any group it was in; a group left without nodes is removed </summary>
        internal static DialogueGroup CreateGroup(DialogueAsset _asset, string _title, IEnumerable<string> _nodeIds) {
            if (_asset == null) return null;

            Begin(_asset, GroupUndo);
            DialogueGroup _group = new DialogueGroup { Id = DialogueIds.New(), Title = _title ?? "" };
            _asset.GroupList.Add(_group);
            AssignNodes(_asset, _group, _nodeIds);
            _asset.EnsureIds();
            End(_asset);
            return _group;
        }

        /// <summary> Replaces the nodes of the group. Nodes taken from another group leave it; a group left without nodes is removed </summary>
        internal static void SetGroupNodes(DialogueAsset _asset, string _groupId, IEnumerable<string> _nodeIds) {
            DialogueGroup _group = FindGroup(_asset, _groupId);
            if (_group == null) return;

            Begin(_asset, GroupUndo);
            AssignNodes(_asset, _group, _nodeIds);
            End(_asset);
        }

        internal static void SetGroup(DialogueAsset _asset, string _groupId, string _title, Color _color) {
            DialogueGroup _group = FindGroup(_asset, _groupId);
            if (_group == null) return;

            Begin(_asset, GroupUndo);
            _group.Title = _title ?? "";
            _group.Color = _color;
            End(_asset);
        }

        internal static DialogueNote CreateNote(DialogueAsset _asset, Rect _rect) {
            if (_asset == null) return null;

            Begin(_asset, NoteUndo);
            DialogueNote _note = new DialogueNote { Id = DialogueIds.New(), Rect = _rect };
            _asset.NoteList.Add(_note);
            _asset.EnsureIds();
            End(_asset);
            return _note;
        }

        internal static void SetNote(DialogueAsset _asset, string _noteId, string _text, Rect _rect) {
            DialogueNote _note = FindNote(_asset, _noteId);
            if (_note == null) return;

            Begin(_asset, NoteUndo);
            _note.Text = _text ?? "";
            _note.Rect = _rect;
            End(_asset);
        }
        #endregion

        // ==================== LAYOUT ====================
        #region Layout
        /// <summary> Ways to line up a set of elements </summary>
        internal enum Arrangement { Left, Center, Right, Top, Middle, Bottom, DistributeHorizontally, DistributeVertically }

        /// <summary> The dialogue has more than one node and all of them sit at the origin, as dialogues made without the graph window do </summary>
        internal static bool NeedsAutoLayout(DialogueAsset _asset) {
            if (_asset == null) return false;

            int _count = 0;
            for (int _i = 0; _i < _asset.Nodes.Count; _i++) {
                if (_asset.Nodes[_i] == null) continue;
                if (_asset.Nodes[_i].Position != Vector2.zero) return false;
                _count++;
            }
            return _count > 1;
        }

        /// <summary> Places the nodes left to right by their distance from the start node, one column per step, in output order inside each column. Nodes the start node never reaches go in a last column </summary>
        internal static void AutoLayout(DialogueAsset _asset) {
            if (_asset == null) return;

            List<List<DialogueNode>> _columns = new List<List<DialogueNode>>();
            Dictionary<DialogueNode, int> _depths = new Dictionary<DialogueNode, int>();
            Queue<DialogueNode> _queue = new Queue<DialogueNode>();
            List<NodeOutput> _outputs = new List<NodeOutput>();

            StartNode _start = _asset.Start;
            if (_start != null) {
                _depths.Add(_start, 0);
                _queue.Enqueue(_start);
            }
            while (_queue.Count > 0) {
                DialogueNode _node = _queue.Dequeue();
                int _depth = _depths[_node];
                if (_columns.Count <= _depth) _columns.Add(new List<DialogueNode>());
                _columns[_depth].Add(_node);

                _outputs.Clear();
                DialogueValidator.CollectUsedOutputs(_node, _outputs);
                for (int _o = 0; _o < _outputs.Count; _o++) {
                    if (_outputs[_o] == null || !_outputs[_o].IsConnected) continue;
                    DialogueNode _target = _asset.Find(_outputs[_o].Target);
                    // A node already placed keeps its column, so cycles end here
                    if (_target == null || _depths.ContainsKey(_target)) continue;
                    _depths.Add(_target, _depth + 1);
                    _queue.Enqueue(_target);
                }
            }

            List<DialogueNode> _unreached = new List<DialogueNode>();
            for (int _i = 0; _i < _asset.Nodes.Count; _i++) {
                if (_asset.Nodes[_i] != null && !_depths.ContainsKey(_asset.Nodes[_i])) _unreached.Add(_asset.Nodes[_i]);
            }
            if (_unreached.Count > 0) _columns.Add(_unreached);

            Begin(_asset, AutoLayoutUndo);
            for (int _c = 0; _c < _columns.Count; _c++) {
                for (int _r = 0; _r < _columns[_c].Count; _r++) _columns[_c][_r].Position = new Vector2(_c * LayoutColumnWidth, _r * LayoutRowHeight);
            }
            End(_asset);
            Undo.SetCurrentGroupName(AutoLayoutUndo);
        }

        /// <summary> The new top left corner of each rect so they line up as asked. Distributing keeps the first and last rects in place and leaves equal gaps between all of them </summary>
        internal static IReadOnlyDictionary<string, Vector2> Arrange(IReadOnlyDictionary<string, Rect> _rects, Arrangement _mode) {
            Dictionary<string, Vector2> _positions = new Dictionary<string, Vector2>();
            if (_rects == null || _rects.Count == 0) return _positions;

            if (_mode == Arrangement.DistributeHorizontally || _mode == Arrangement.DistributeVertically) {
                Distribute(_rects, _mode == Arrangement.DistributeHorizontally, _positions);
                return _positions;
            }

            float _xMin = float.MaxValue, _xMax = float.MinValue, _yMin = float.MaxValue, _yMax = float.MinValue;
            foreach (Rect _rect in _rects.Values) {
                _xMin = Mathf.Min(_xMin, _rect.xMin);
                _xMax = Mathf.Max(_xMax, _rect.xMax);
                _yMin = Mathf.Min(_yMin, _rect.yMin);
                _yMax = Mathf.Max(_yMax, _rect.yMax);
            }

            foreach (KeyValuePair<string, Rect> _pair in _rects) {
                Rect _rect = _pair.Value;
                Vector2 _position = _rect.position;
                switch (_mode) {
                    case Arrangement.Left: _position.x = _xMin; break;
                    case Arrangement.Center: _position.x = (_xMin + _xMax) * 0.5f - _rect.width * 0.5f; break;
                    case Arrangement.Right: _position.x = _xMax - _rect.width; break;
                    case Arrangement.Top: _position.y = _yMin; break;
                    case Arrangement.Middle: _position.y = (_yMin + _yMax) * 0.5f - _rect.height * 0.5f; break;
                    case Arrangement.Bottom: _position.y = _yMax - _rect.height; break;
                }
                _positions[_pair.Key] = _position;
            }
            return _positions;
        }
        #endregion

        // ==================== LOOKUP ====================
        #region Lookup
        /// <summary> The output with the id, in any node. Null when none has it </summary>
        internal static NodeOutput FindOutput(DialogueAsset _asset, string _outputId) {
            if (_asset == null || string.IsNullOrEmpty(_outputId)) return null;

            for (int _i = 0; _i < _asset.Nodes.Count; _i++) {
                if (_asset.Nodes[_i] == null) continue;
                outputBuffer.Clear();
                _asset.Nodes[_i].CollectOutputs(outputBuffer);
                for (int _o = 0; _o < outputBuffer.Count; _o++) {
                    if (outputBuffer[_o] != null && outputBuffer[_o].Id == _outputId) return outputBuffer[_o];
                }
            }
            return null;
        }

        /// <summary> The node that holds the output with the id. Null when none has it </summary>
        internal static DialogueNode OwnerOf(DialogueAsset _asset, string _outputId) {
            if (_asset == null || string.IsNullOrEmpty(_outputId)) return null;

            for (int _i = 0; _i < _asset.Nodes.Count; _i++) {
                if (_asset.Nodes[_i] == null) continue;
                outputBuffer.Clear();
                _asset.Nodes[_i].CollectOutputs(outputBuffer);
                for (int _o = 0; _o < outputBuffer.Count; _o++) {
                    if (outputBuffer[_o] != null && outputBuffer[_o].Id == _outputId) return _asset.Nodes[_i];
                }
            }
            return null;
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        private static void Begin(DialogueAsset _asset, string _undoName) {
            Undo.RecordObject(_asset, _undoName);
        }

        private static void End(DialogueAsset _asset) {
            EditorUtility.SetDirty(_asset);
        }

        private static void MoveItem<T>(DialogueAsset _asset, List<T> _list, int _from, int _to, string _undoName) {
            if (_from < 0 || _from >= _list.Count || _to < 0 || _to >= _list.Count || _from == _to) return;

            Begin(_asset, _undoName);
            T _item = _list[_from];
            _list.RemoveAt(_from);
            _list.Insert(_to, _item);
            End(_asset);
        }

        private static void AssignNodes(DialogueAsset _asset, DialogueGroup _group, IEnumerable<string> _nodeIds) {
            List<string> _members = new List<string>();
            if (_nodeIds != null) {
                foreach (string _id in _nodeIds) {
                    if (_asset.Find(_id) != null && !_members.Contains(_id)) _members.Add(_id);
                }
            }

            // A group is drawn around its nodes, so one whose last nodes move away goes too
            for (int _g = _asset.GroupList.Count - 1; _g >= 0; _g--) {
                DialogueGroup _other = _asset.GroupList[_g];
                if (_other == null || _other == _group) continue;
                if (_other.NodeIdList.RemoveAll(_id => _members.Contains(_id)) > 0 && _other.NodeIdList.Count == 0) _asset.GroupList.RemoveAt(_g);
            }

            _group.NodeIdList.Clear();
            _group.NodeIdList.AddRange(_members);
        }

        private static void Distribute(IReadOnlyDictionary<string, Rect> _rects, bool _horizontal, Dictionary<string, Vector2> _into) {
            List<KeyValuePair<string, Rect>> _ordered = _rects.OrderBy(_pair => _horizontal ? _pair.Value.x : _pair.Value.y).ToList();
            for (int _i = 0; _i < _ordered.Count; _i++) _into[_ordered[_i].Key] = _ordered[_i].Value.position;
            if (_ordered.Count < 3) return;

            float Start(Rect _rect) => _horizontal ? _rect.xMin : _rect.yMin;
            float Size(Rect _rect) => _horizontal ? _rect.width : _rect.height;

            float _inner = 0f;
            for (int _i = 1; _i < _ordered.Count - 1; _i++) _inner += Size(_ordered[_i].Value);
            Rect _first = _ordered[0].Value;
            float _firstEnd = Start(_first) + Size(_first);
            float _gap = (Start(_ordered[_ordered.Count - 1].Value) - _firstEnd - _inner) / (_ordered.Count - 1);

            float _cursor = _firstEnd + _gap;
            for (int _i = 1; _i < _ordered.Count - 1; _i++) {
                Rect _rect = _ordered[_i].Value;
                _into[_ordered[_i].Key] = _horizontal ? new Vector2(_cursor, _rect.y) : new Vector2(_rect.x, _cursor);
                _cursor += Size(_rect) + _gap;
            }
        }

        private static DialogueGroup FindGroup(DialogueAsset _asset, string _id) {
            if (_asset == null || string.IsNullOrEmpty(_id)) return null;
            return _asset.GroupList.Find(_group => _group != null && _group.Id == _id);
        }

        private static DialogueNote FindNote(DialogueAsset _asset, string _id) {
            if (_asset == null || string.IsNullOrEmpty(_id)) return null;
            return _asset.NoteList.Find(_note => _note != null && _note.Id == _id);
        }
        #endregion
    }
}
