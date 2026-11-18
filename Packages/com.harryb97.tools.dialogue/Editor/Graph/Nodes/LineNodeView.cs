using UnityEditor.Experimental.GraphView;

namespace HarryTools.Dialogue.Editor {
    /// <summary> A line said by a character </summary>
    internal sealed class LineNodeView : DialogueNodeView {
        internal LineNodeView(LineNode _node, IEdgeConnectorListener _listener) : base(_node, _listener) { }

        internal LineNode NodeData => (LineNode)Data;

        protected override string Title => "Line";
        protected override string TypeClass => "dialogue-node--line";
    }
}
