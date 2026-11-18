using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace HarryTools.Dialogue.Editor {
    /// <summary> A line said by a character: shows the character and the start of its text, edited in the side panel </summary>
    internal sealed class LineNodeView : DialogueNodeView {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const int PreviewLines = 2;
        private const string NoCharacter = "No character";
        private const string NoCharacterClass = "dialogue-node__character--none";
        #endregion

        #region Public Variables
        internal LineNode NodeData => (LineNode)Data;

        /// <summary> The character who says the line, or a hint when it has none </summary>
        internal Label CharacterLabel { get; }

        /// <summary> The line's text in the source locale, cut to two lines </summary>
        internal ClippedLabel Preview { get; }
        #endregion

        // ==================== CONSTRUCTION ====================
        #region Construction
        internal LineNodeView(DialogueAsset _asset, LineNode _node, IEdgeConnectorListener _listener) : base(_asset, _node, _listener) {
            CharacterLabel = new Label { pickingMode = PickingMode.Ignore };
            CharacterLabel.AddToClassList("dialogue-node__character");
            Preview = new ClippedLabel(PreviewLines);

            VisualElement _content = new VisualElement();
            _content.AddToClassList("dialogue-node__content");
            _content.Add(CharacterLabel);
            _content.Add(Preview);
            // Between the input and the output, so the ports sit on the edges next to the text
            topContainer.Insert(topContainer.IndexOf(outputContainer), _content);
            outputContainer.style.flexGrow = 0f;
            outputContainer.style.flexShrink = 0f;
            _content.style.minWidth = 0f;
        }
        #endregion

        // ==================== METHODS ====================
        #region Methods
        internal override void Refresh() {
            base.Refresh();
            DialogueCharacter _character = NodeData.Character;
            CharacterLabel.text = _character != null ? _character.name : NoCharacter;
            CharacterLabel.EnableInClassList(NoCharacterClass, _character == null);
            Preview.FullText = PreviewText(NodeData.Text, false);
        }

        protected override string Title => "Line";
        protected override string TypeClass => "dialogue-node--line";

        protected override void CollectIndicators(List<string> _into) {
            LineNode _line = NodeData;
            if (ActionCount(_line.Before?.Actions) + ActionCount(_line.OnStart?.Actions) + ActionCount(_line.OnEnd?.Actions) > 0) _into.Add("Actions");
            if (_line.Events != null && _line.Events.Entries.Count > 0) _into.Add("Events");
            if (_line.DelayBefore > 0f || _line.DelayAfter > 0f) _into.Add("Delay");
            if (_line.OverrideLifetime) _into.Add("Lifetime");
            if (_line.OverrideAdvance) _into.Add("Advance");
        }
        #endregion
    }
}
