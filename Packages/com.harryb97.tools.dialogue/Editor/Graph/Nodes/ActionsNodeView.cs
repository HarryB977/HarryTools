using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Runs a list of actions. Shows how many and whether the dialogue waits for them </summary>
    internal sealed class ActionsNodeView : DialogueNodeView {
        internal ActionsNodeView(DialogueAsset _asset, ActionsNode _node, IEdgeConnectorListener _listener) : base(_asset, _node, _listener) { }

        internal ActionsNode NodeData => (ActionsNode)Data;

        protected override string Title => "Actions";
        protected override string TypeClass => "dialogue-node--actions";

        protected override void CollectIndicators(List<string> _into) {
            _into.Add(Count(ActionCount(NodeData.Actions), "action"));
            if (NodeData.WaitForCompletion) _into.Add("Waits");
        }
    }
}
