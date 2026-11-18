using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UIElements;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Canvas view of one dialogue node: its title, an input port, one output port per <see cref="NodeOutput"/> and a short summary of its content. Built from the node and rebuilt with the graph </summary>
    internal abstract class DialogueNodeView : Node {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string NodeClass = "dialogue-node";
        private const string IndicatorsClass = "dialogue-node__indicators";
        private const string IndicatorClass = "dialogue-node__indicator";
        private const string WarningClass = "dialogue-node--warning";
        private const string TextUndo = "Edit Dialogue Text";

        private readonly List<(string OutputId, Port Port)> outputs = new List<(string OutputId, Port Port)>();
        private readonly List<string> indicators = new List<string>();
        private readonly VisualElement indicatorRow;
        #endregion

        #region Public Variables
        internal string NodeId { get; }

        internal DialogueNode Data { get; }

        /// <summary> The dialogue the node belongs to. Text typed on the node is written to its table </summary>
        internal DialogueAsset Asset { get; }

        /// <summary> The port other outputs connect to. Null for the start node </summary>
        internal Port Input { get; private set; }

        /// <summary> One port per output shown on the canvas, in the node's output order </summary>
        internal IReadOnlyList<(string OutputId, Port Port)> Outputs => outputs;

        /// <summary> The short markers shown under the node, such as "Actions" or "Delay" </summary>
        internal IReadOnlyList<string> Indicators => indicators;
        #endregion

        // ==================== CONSTRUCTION ====================
        #region Construction
        protected DialogueNodeView(DialogueAsset _asset, DialogueNode _node, IEdgeConnectorListener _listener) {
            Asset = _asset;
            Data = _node;
            NodeId = _node.Id;
            viewDataKey = _node.Id;
            AddToClassList(NodeClass);
            AddToClassList(TypeClass);

            if (HasInput) {
                Input = DialoguePort.Create(Direction.Input, Port.Capacity.Multi, _listener);
                Input.portName = "";
                inputContainer.Add(Input);
            }

            List<NodeOutput> _nodeOutputs = new List<NodeOutput>();
            _node.CollectOutputs(_nodeOutputs);
            for (int _i = 0; _i < _nodeOutputs.Count; _i++) {
                NodeOutput _output = _nodeOutputs[_i];
                if (_output == null || !ShowsOutput(_output)) continue;
                Port _port = DialoguePort.Create(Direction.Output, Port.Capacity.Single, _listener);
                _port.userData = _output.Id;
                outputContainer.Add(_port);
                outputs.Add((_output.Id, _port));
            }

            indicatorRow = new VisualElement();
            indicatorRow.AddToClassList(IndicatorsClass);
            extensionContainer.Add(indicatorRow);
            expanded = true;

            SetPosition(new Rect(_node.Position, Vector2.zero));
        }

        /// <summary> The view for the node's type. Types without their own view get a plain one with title and ports </summary>
        internal static DialogueNodeView Create(DialogueAsset _asset, DialogueNode _node, IEdgeConnectorListener _listener) {
            DialogueNodeView _view;
            switch (_node) {
                case StartNode _start: _view = new StartNodeView(_asset, _start, _listener); break;
                case LineNode _line: _view = new LineNodeView(_asset, _line, _listener); break;
                case ChoiceNode _choice: _view = new ChoiceNodeView(_asset, _choice, _listener); break;
                case BranchNode _branch: _view = new BranchNodeView(_asset, _branch, _listener); break;
                case ActionsNode _actions: _view = new ActionsNodeView(_asset, _actions, _listener); break;
                case JumpNode _jump: _view = new JumpNodeView(_asset, _jump, _listener); break;
                case EndNode _end: _view = new EndNodeView(_asset, _end, _listener); break;
                default: _view = new PlainNodeView(_asset, _node, _listener); break;
            }

            // After construction, so a derived view can fill its own fields before the first refresh
            _view.Refresh();
            _view.RefreshExpandedState();
            _view.RefreshPorts();
            return _view;
        }
        #endregion

        // ==================== METHODS ====================
        #region Methods
        /// <summary> Updates what the node shows from its data. Ports stay as they are: a change of outputs needs a rebuild of the graph (see <see cref="OutputsChanged"/>) </summary>
        internal virtual void Refresh() {
            title = Title;
            for (int _i = 0; _i < outputs.Count; _i++) outputs[_i].Port.portName = OutputLabel(_i);

            indicators.Clear();
            CollectIndicators(indicators);
            indicatorRow.Clear();
            for (int _i = 0; _i < indicators.Count; _i++) {
                Label _label = new Label(indicators[_i]);
                _label.AddToClassList(IndicatorClass);
                indicatorRow.Add(_label);
            }
            indicatorRow.style.display = indicators.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary> Marks the node as having warnings, with their text as tooltip. Null or empty clears the mark </summary>
        internal void SetWarnings(string _text) {
            bool _has = !string.IsNullOrEmpty(_text);
            EnableInClassList(WarningClass, _has);
            tooltip = _has ? _text : "";
        }

        /// <summary> Whether the node carries the warning mark </summary>
        internal bool HasWarningMark => ClassListContains(WarningClass);

        /// <summary> The node's data now has other outputs than the ports shown, so the graph has to be rebuilt </summary>
        internal bool OutputsChanged() {
            List<NodeOutput> _nodeOutputs = new List<NodeOutput>();
            Data.CollectOutputs(_nodeOutputs);
            int _shown = 0;
            for (int _i = 0; _i < _nodeOutputs.Count; _i++) {
                NodeOutput _output = _nodeOutputs[_i];
                if (_output == null || !ShowsOutput(_output)) continue;
                if (_shown >= outputs.Count || outputs[_shown].OutputId != _output.Id) return true;
                _shown++;
            }
            return _shown != outputs.Count;
        }

        /// <summary> Where the node is placed on the canvas. Read from its style, which is up to date before the next layout pass </summary>
        internal Vector2 CanvasPosition => new Vector2(style.left.value.value, style.top.value.value);

        /// <summary> The port of the output with the id. Null when the node does not show it </summary>
        internal Port FindOutputPort(string _outputId) {
            for (int _i = 0; _i < outputs.Count; _i++) {
                if (outputs[_i].OutputId == _outputId) return outputs[_i].Port;
            }
            return null;
        }
        #endregion

        // ==================== OVERRIDABLE ====================
        #region Overridable
        // TypeClass, HasInput and ShowsOutput are read by the base constructor, so they may only use Data
        protected abstract string Title { get; }

        /// <summary> USS class of the node type, for its header color </summary>
        protected abstract string TypeClass { get; }

        protected virtual bool HasInput => true;

        /// <summary> Whether the output gets a port on the canvas </summary>
        protected virtual bool ShowsOutput(NodeOutput _output) => true;

        /// <summary> Label of the output at the index of <see cref="Outputs"/> </summary>
        protected virtual string OutputLabel(int _index) => "";

        /// <summary> Adds the markers shown under the node </summary>
        protected virtual void CollectIndicators(List<string> _into) { }
        #endregion

        // ==================== HELPERS ====================
        #region Helpers
        /// <summary> Writes a text of the node to the source locale table as one undo step </summary>
        protected void WriteText(LocalizedString _target, string _key, string _text) {
            if (Asset == null || _target == null || string.IsNullOrEmpty(_key)) return;

            Undo.IncrementCurrentGroup();
            int _group = Undo.GetCurrentGroup();
            DialogueTables.SetSourceText(Asset, _target, _key, _text);
            Undo.SetCurrentGroupName(TextUndo);
            Undo.CollapseUndoOperations(_group);
        }

        /// <summary> A text field on the node for a dialogue text in the source locale. Disabled when the project has no source locale </summary>
        protected NodeTextField CreateTextField(System.Func<LocalizedString> _target, System.Func<string> _key) {
            NodeTextField _field = new NodeTextField(() => DialogueTables.GetSourceText(_target()), _text => WriteText(_target(), _key(), _text)) { Key = _key() };
            if (DialogueTables.SourceLocale() == null) {
                _field.SetEnabled(false);
                _field.tooltip = "No source locale. Add locales in the Localization settings to write text.";
            }
            return _field;
        }

        protected static string Count(int _count, string _singular) {
            return _count + " " + _singular + (_count == 1 ? "" : "s");
        }

        protected static int ActionCount(HarryTools.Actions.ActionList _list) {
            if (_list == null || _list.Actions == null) return 0;
            int _count = 0;
            for (int _i = 0; _i < _list.Actions.Length; _i++) {
                if (_list.Actions[_i] != null) _count++;
            }
            return _count;
        }

        protected static int ConditionCount(HarryTools.Conditions.ConditionList _list) {
            if (_list == null || _list.Conditions == null) return 0;
            int _count = 0;
            for (int _i = 0; _i < _list.Conditions.Length; _i++) {
                if (_list.Conditions[_i] != null) _count++;
            }
            return _count;
        }
        #endregion

        // ==================== PORT ====================
        #region Port
        /// <summary> A port that reports dropped connections to the graph instead of connecting them itself </summary>
        private sealed class DialoguePort : Port {
            private DialoguePort(Direction _direction, Capacity _capacity) : base(Orientation.Horizontal, _direction, _capacity, typeof(bool)) { }

            internal static DialoguePort Create(Direction _direction, Capacity _capacity, IEdgeConnectorListener _listener) {
                DialoguePort _port = new DialoguePort(_direction, _capacity);
                if (_listener != null) {
                    _port.m_EdgeConnector = new EdgeConnector<Edge>(_listener);
                    _port.AddManipulator(_port.m_EdgeConnector);
                }
                return _port;
            }
        }

        private sealed class PlainNodeView : DialogueNodeView {
            internal PlainNodeView(DialogueAsset _asset, DialogueNode _node, IEdgeConnectorListener _listener) : base(_asset, _node, _listener) { }

            protected override string Title => Data.GetType().Name;
            protected override string TypeClass => "dialogue-node--plain";
        }
        #endregion
    }
}
