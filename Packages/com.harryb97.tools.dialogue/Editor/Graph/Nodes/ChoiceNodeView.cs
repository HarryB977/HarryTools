using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Offers options: one output per option with its text editable next to it, then the no answer output </summary>
    internal sealed class ChoiceNodeView : DialogueNodeView {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string NoAnswerLabel = "No Answer";

        private readonly List<NodeTextField> optionFields = new List<NodeTextField>();
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
                NodeTextField _field = CreateTextField(() => _option.Text, () => _option.Id);
                _field.AddToClassList("dialogue-node__option");
                Outputs[_port].Port.Add(_field);
                optionFields.Add(_field);
                _port++;
            }
        }
        #endregion

        // ==================== METHODS ====================
        #region Methods
        /// <summary> The text field of the option at the index among the options shown </summary>
        internal NodeTextField OptionField(int _index) => optionFields[_index];

        internal override void Refresh() {
            base.Refresh();
            for (int _i = 0; _i < optionFields.Count; _i++) optionFields[_i].Reload();
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
