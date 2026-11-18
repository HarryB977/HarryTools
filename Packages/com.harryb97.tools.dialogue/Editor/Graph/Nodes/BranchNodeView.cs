using UnityEditor.Experimental.GraphView;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Takes the first case whose conditions hold: one output per case, then the otherwise output </summary>
    internal sealed class BranchNodeView : DialogueNodeView {
        private const string CaseLabel = "Case ";
        private const string OtherwiseLabel = "Otherwise";

        internal BranchNodeView(BranchNode _node, IEdgeConnectorListener _listener) : base(_node, _listener) { }

        internal BranchNode NodeData => (BranchNode)Data;

        protected override string Title => "Branch";
        protected override string TypeClass => "dialogue-node--branch";

        protected override string OutputLabel(int _index) => _index == Outputs.Count - 1 ? OtherwiseLabel : CaseLabel + (_index + 1);
    }
}
