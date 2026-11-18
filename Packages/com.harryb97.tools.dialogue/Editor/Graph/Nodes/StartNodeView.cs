using UnityEditor.Experimental.GraphView;

namespace HarryTools.Dialogue.Editor {
    /// <summary> The entry of the dialogue: a compact node with one output. It cannot be deleted or copied </summary>
    internal sealed class StartNodeView : DialogueNodeView {
        internal StartNodeView(DialogueAsset _asset, StartNode _node, IEdgeConnectorListener _listener) : base(_asset, _node, _listener) {
            capabilities &= ~(Capabilities.Deletable | Capabilities.Copiable);
        }

        internal StartNode NodeData => (StartNode)Data;

        protected override string Title => "Start";
        protected override string TypeClass => "dialogue-node--start";
        protected override bool IsCompact => true;
        protected override bool HasInput => false;
        protected override string BaseTooltip => "Entry point of the dialogue";
    }
}
