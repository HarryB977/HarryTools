using UnityEditor.Experimental.GraphView;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Runs a list of actions </summary>
    internal sealed class ActionsNodeView : DialogueNodeView {
        internal ActionsNodeView(ActionsNode _node, IEdgeConnectorListener _listener) : base(_node, _listener) { }

        internal ActionsNode NodeData => (ActionsNode)Data;

        protected override string Title => "Actions";
        protected override string TypeClass => "dialogue-node--actions";
    }
}
