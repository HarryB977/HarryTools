using UnityEditor.Experimental.GraphView;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Moves the conversation into another dialogue. Its output only exists on the canvas when it comes back </summary>
    internal sealed class JumpNodeView : DialogueNodeView {
        internal JumpNodeView(JumpNode _node, IEdgeConnectorListener _listener) : base(_node, _listener) { }

        internal JumpNode NodeData => (JumpNode)Data;

        protected override string Title => "Jump";
        protected override string TypeClass => "dialogue-node--jump";

        protected override bool ShowsOutput(NodeOutput _output) => NodeData.Mode == JumpMode.CallAndReturn;
    }
}
