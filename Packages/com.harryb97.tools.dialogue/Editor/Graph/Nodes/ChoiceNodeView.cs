using UnityEditor.Experimental.GraphView;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Offers options: one output per option, then the no answer output </summary>
    internal sealed class ChoiceNodeView : DialogueNodeView {
        private const string OptionLabel = "Option ";
        private const string NoAnswerLabel = "No Answer";

        internal ChoiceNodeView(ChoiceNode _node, IEdgeConnectorListener _listener) : base(_node, _listener) { }

        internal ChoiceNode NodeData => (ChoiceNode)Data;

        protected override string Title => "Choice";
        protected override string TypeClass => "dialogue-node--choice";

        protected override string OutputLabel(int _index) => _index == Outputs.Count - 1 ? NoAnswerLabel : OptionLabel + (_index + 1);
    }
}
