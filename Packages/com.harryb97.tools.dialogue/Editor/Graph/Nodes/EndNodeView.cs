using UnityEditor.Experimental.GraphView;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Ends the dialogue: a compact node with one input </summary>
    internal sealed class EndNodeView : DialogueNodeView {
        internal EndNodeView(DialogueAsset _asset, EndNode _node, IEdgeConnectorListener _listener) : base(_asset, _node, _listener) { }

        internal EndNode NodeData => (EndNode)Data;

        protected override string Title => "End";
        protected override string TypeClass => "dialogue-node--end";
        protected override bool IsCompact => true;
        protected override string BaseTooltip => "Ends the dialogue here. An unconnected output also ends it.";
    }
}
