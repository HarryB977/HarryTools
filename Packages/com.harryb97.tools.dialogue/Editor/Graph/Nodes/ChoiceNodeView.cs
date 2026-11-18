using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Offers options: one output per option with the start of its text next to it, then the no answer output </summary>
    internal sealed class ChoiceNodeView : DialogueNodeView {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string NoAnswerLabel = "No Answer";
        private const string OptionPortClass = "dialogue-node__option";

        private readonly List<(ChoiceOption Option, ClippedLabel Preview)> optionPreviews = new List<(ChoiceOption, ClippedLabel)>();
        #endregion

        #region Public Variables
        internal ChoiceNode NodeData => (ChoiceNode)Data;
        #endregion

        // ==================== CONSTRUCTION ====================
        #region Construction
        internal ChoiceNodeView(DialogueAsset _asset, ChoiceNode _node, IEdgeConnectorListener _listener) : base(_asset, _node, _listener) {
            // Every output but the last one belongs to an option, in the same order as the non null options
            int _port = 0;
            for (int _i = 0; _i < _node.Options.Count && _port < Outputs.Count - 1; _i++) {
                ChoiceOption _option = _node.Options[_i];
                if (_option == null) continue;
                ClippedLabel _preview = new ClippedLabel(1);
                Outputs[_port].Port.AddToClassList(OptionPortClass);
                Outputs[_port].Port.style.minWidth = 0f;
                Outputs[_port].Port.Add(_preview);
                optionPreviews.Add((_option, _preview));
                _port++;
            }
        }
        #endregion

        // ==================== METHODS ====================
        #region Methods
        /// <summary> The text shown next to the option at the index among the options shown </summary>
        internal ClippedLabel OptionPreview(int _index) => optionPreviews[_index].Preview;

        internal override void Refresh() {
            base.Refresh();
            for (int _i = 0; _i < optionPreviews.Count; _i++) optionPreviews[_i].Preview.FullText = PreviewText(optionPreviews[_i].Option.Text, true);
        }

        protected override string Title => "Choice";
        protected override string TypeClass => "dialogue-node--choice";

        protected override string OutputLabel(int _index) => _index == Outputs.Count - 1 ? NoAnswerLabel : "";

        protected override void CollectIndicators(List<string> _into) {
            if (NodeData.UseTimer) _into.Add("Timer " + NodeData.TimerSeconds.ToString("0.#") + "s");
        }
        #endregion
    }
}
