using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using HarryTools.TextEffects;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UIElements;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Canvas view of one dialogue node: its title, an input port, one output port per <see cref="NodeOutput"/> and a short read only summary of its content. Built from the node and rebuilt with the graph; its data is edited in the side panel </summary>
    internal abstract class DialogueNodeView : Node {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string NodeClass = "dialogue-node";
        private const string ContentNodeClass = "dialogue-node--content";
        private const string CompactNodeClass = "dialogue-node--compact";
        private const string IndicatorsClass = "dialogue-node__indicators";
        private const string IndicatorClass = "dialogue-node__indicator";
        private const string WarningClass = "dialogue-node--warning";
        private const string CurrentClass = "current";
        // Any tag left after the text effects parser, so the preview is plain text
        private static readonly Regex MarkupTag = new Regex("<[^<>]*>");
        private static readonly Regex LineBreaks = new Regex(@"\s*\n\s*");

        private readonly List<(string OutputId, Port Port)> outputs = new List<(string OutputId, Port Port)>();
        private readonly List<string> indicators = new List<string>();
        private readonly VisualElement indicatorRow;
        #endregion

        #region Public Variables
        internal string NodeId { get; }

        internal DialogueNode Data { get; }

        /// <summary> The dialogue the node belongs to, whose table holds the node's text </summary>
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
            AddToClassList(IsCompact ? CompactNodeClass : ContentNodeClass);
            tooltip = BaseTooltip;

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

            // A compact node is only its title with the port on its side
            if (IsCompact) {
                if (Input != null) titleContainer.Insert(0, Input);
                for (int _i = 0; _i < outputs.Count; _i++) titleContainer.Add(outputs[_i].Port);
            } else {
                // The input takes only its port's room, so outputs and their texts get the rest of the width
                // Inline, as the graph view's own sheets win over the package's for these containers. Long texts shrink instead of widening the node
                inputContainer.style.flexGrow = 0f;
                outputContainer.style.flexGrow = 1f;
                outputContainer.style.flexShrink = 1f;
                outputContainer.style.minWidth = 0f;
                topContainer.style.overflow = Overflow.Hidden;
            }

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

        /// <summary> Marks the node as having warnings, with their text added to its tooltip. Null or empty clears the mark </summary>
        internal void SetWarnings(string _text) {
            bool _has = !string.IsNullOrEmpty(_text);
            EnableInClassList(WarningClass, _has);
            if (!_has) tooltip = BaseTooltip;
            else tooltip = string.IsNullOrEmpty(BaseTooltip) ? _text : BaseTooltip + "\n" + _text;
        }

        /// <summary> Whether the node carries the warning mark </summary>
        internal bool HasWarningMark => ClassListContains(WarningClass);

        /// <summary> Lights the node up as the one a running dialogue is on </summary>
        internal void SetCurrent(bool _current) {
            EnableInClassList(CurrentClass, _current);
        }

        /// <summary> Leaves the node to look at: it cannot be moved or deleted. Undone by building the view again </summary>
        internal void MakeReadOnly() {
            capabilities &= ~(Capabilities.Movable | Capabilities.Deletable);
        }

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
        // TypeClass, IsCompact, HasInput, ShowsOutput and BaseTooltip are read by the base constructor, so they may only use Data
        protected abstract string Title { get; }

        /// <summary> Drawn as its title and port only, without content. Content nodes all share one width </summary>
        protected virtual bool IsCompact => false;

        /// <summary> USS class of the node type, for its header color </summary>
        protected abstract string TypeClass { get; }

        protected virtual bool HasInput => true;

        /// <summary> The tooltip of the node without warnings </summary>
        protected virtual string BaseTooltip => "";

        /// <summary> Whether the output gets a port on the canvas </summary>
        protected virtual bool ShowsOutput(NodeOutput _output) => true;

        /// <summary> Label of the output at the index of <see cref="Outputs"/> </summary>
        protected virtual string OutputLabel(int _index) => "";

        /// <summary> Adds the markers shown under the node </summary>
        protected virtual void CollectIndicators(List<string> _into) { }
        #endregion

        // ==================== HELPERS ====================
        #region Helpers
        /// <summary> The text in the source locale as plain text: effect and markup tags removed, trimmed. With one line, line breaks become spaces. Empty when there is no text </summary>
        internal static string PreviewText(LocalizedString _text, bool _oneLine) {
            string _source = DialogueTables.GetSourceText(_text);
            if (string.IsNullOrEmpty(_source)) return "";

            string _plain;
            try {
                _plain = TextParser.Parse(_source, TextEffectsSettings.Current).TmpText ?? "";
            } catch (Exception) {
                // A preview never breaks the canvas; the validator reports broken markup
                _plain = _source;
            }
            _plain = MarkupTag.Replace(_plain, "").Replace("\r", "");
            if (_oneLine) _plain = LineBreaks.Replace(_plain, " ");
            return _plain.Trim();
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
