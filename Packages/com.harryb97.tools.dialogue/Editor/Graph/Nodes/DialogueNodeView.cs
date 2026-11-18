using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Canvas view of one dialogue node: its title, an input port and one output port per <see cref="NodeOutput"/>. Built from the node and rebuilt with the graph </summary>
    internal abstract class DialogueNodeView : Node {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string NodeClass = "dialogue-node";

        private readonly List<(string OutputId, Port Port)> outputs = new List<(string OutputId, Port Port)>();
        #endregion

        #region Public Variables
        internal string NodeId { get; }

        internal DialogueNode Data { get; }

        /// <summary> The port other outputs connect to. Null for the start node </summary>
        internal Port Input { get; private set; }

        /// <summary> One port per output shown on the canvas, in the node's output order </summary>
        internal IReadOnlyList<(string OutputId, Port Port)> Outputs => outputs;
        #endregion

        // ==================== CONSTRUCTION ====================
        #region Construction
        protected DialogueNodeView(DialogueNode _node, IEdgeConnectorListener _listener) {
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

            SetPosition(new Rect(_node.Position, Vector2.zero));
        }

        /// <summary> The view for the node's type. Types without their own view get a plain one with title and ports </summary>
        internal static DialogueNodeView Create(DialogueNode _node, IEdgeConnectorListener _listener) {
            DialogueNodeView _view;
            switch (_node) {
                case StartNode _start: _view = new StartNodeView(_start, _listener); break;
                case LineNode _line: _view = new LineNodeView(_line, _listener); break;
                case ChoiceNode _choice: _view = new ChoiceNodeView(_choice, _listener); break;
                case BranchNode _branch: _view = new BranchNodeView(_branch, _listener); break;
                case ActionsNode _actions: _view = new ActionsNodeView(_actions, _listener); break;
                case JumpNode _jump: _view = new JumpNodeView(_jump, _listener); break;
                case EndNode _end: _view = new EndNodeView(_end, _listener); break;
                default: _view = new PlainNodeView(_node, _listener); break;
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
        /// <summary> Updates what the node shows from its data. Ports stay as they are: a change of outputs needs a rebuild of the graph </summary>
        internal virtual void Refresh() {
            title = Title;
            for (int _i = 0; _i < outputs.Count; _i++) outputs[_i].Port.portName = OutputLabel(_i);
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
            internal PlainNodeView(DialogueNode _node, IEdgeConnectorListener _listener) : base(_node, _listener) { }

            protected override string Title => Data.GetType().Name;
            protected override string TypeClass => "dialogue-node--plain";
        }
        #endregion
    }
}
