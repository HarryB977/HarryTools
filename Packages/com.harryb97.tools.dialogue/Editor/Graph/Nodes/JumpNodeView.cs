using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Moves the conversation into another dialogue. Shows the target and the mode; a double click opens the target. Its output only exists on the canvas when it comes back </summary>
    internal sealed class JumpNodeView : DialogueNodeView {
        internal JumpNodeView(DialogueAsset _asset, JumpNode _node, IEdgeConnectorListener _listener) : base(_asset, _node, _listener) {
            RegisterCallback<MouseDownEvent>(OnMouseDown);
        }

        internal JumpNode NodeData => (JumpNode)Data;

        protected override string Title => "Jump";
        protected override string TypeClass => "dialogue-node--jump";

        protected override string BaseTooltip => "Double click to open the target dialogue";

        protected override bool ShowsOutput(NodeOutput _output) => NodeData.Mode == JumpMode.CallAndReturn;

        protected override void CollectIndicators(List<string> _into) {
            _into.Add(NodeData.Target != null ? NodeData.Target.name : "No target");
            _into.Add(NodeData.Mode == JumpMode.CallAndReturn ? "Call and Return" : "Continue");
        }

        private void OnMouseDown(MouseDownEvent _event) {
            if (_event.clickCount != 2 || _event.button != 0 || NodeData.Target == null) return;
            _event.StopPropagation();
            // Opened on the next update, so the canvas is not rebuilt in the middle of its own event
            DialogueAsset _target = NodeData.Target;
            UnityEditor.EditorApplication.delayCall += () => DialogueGraphWindow.Open(_target);
        }
    }
}
