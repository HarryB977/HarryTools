using UnityEditor.Experimental.GraphView;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Takes the first case whose conditions hold: one output per case, labelled with its number of conditions, then the otherwise output </summary>
    internal sealed class BranchNodeView : DialogueNodeView {
        private const string OtherwiseLabel = "Otherwise";

        internal BranchNodeView(DialogueAsset _asset, BranchNode _node, IEdgeConnectorListener _listener) : base(_asset, _node, _listener) { }

        internal BranchNode NodeData => (BranchNode)Data;

        protected override string Title => "Branch";
        protected override string TypeClass => "dialogue-node--branch";

        protected override string OutputLabel(int _index) {
            if (_index == Outputs.Count - 1) return OtherwiseLabel;

            // Outputs follow the non null cases in order
            int _shown = 0;
            for (int _i = 0; _i < NodeData.Cases.Count; _i++) {
                BranchCase _case = NodeData.Cases[_i];
                if (_case == null) continue;
                if (_shown == _index) return Count(ConditionCount(_case.Conditions), "condition");
                _shown++;
            }
            return "";
        }
    }
}
