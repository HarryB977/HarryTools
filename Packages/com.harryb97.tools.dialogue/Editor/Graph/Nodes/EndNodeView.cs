using UnityEditor.Experimental.GraphView;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Ends the dialogue </summary>
    internal sealed class EndNodeView : DialogueNodeView {
        internal EndNodeView(EndNode _node, IEdgeConnectorListener _listener) : base(_node, _listener) { }

        internal EndNode NodeData => (EndNode)Data;

        protected override string Title => "End";
        protected override string TypeClass => "dialogue-node--end";
    }
}
