using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace HarryTools.Dialogue.Editor {
    /// <summary> A line said by a character: its character and its text, both editable on the canvas </summary>
    internal sealed class LineNodeView : DialogueNodeView {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string CharacterUndo = "Set Line Character";

        private readonly ObjectField characterField;
        #endregion

        #region Public Variables
        internal LineNode NodeData => (LineNode)Data;

        /// <summary> The line's text in the source locale </summary>
        internal NodeTextField TextField { get; }
        #endregion

        // ==================== CONSTRUCTION ====================
        #region Construction
        internal LineNodeView(DialogueAsset _asset, LineNode _node, IEdgeConnectorListener _listener) : base(_asset, _node, _listener) {
            characterField = new ObjectField { objectType = typeof(DialogueCharacter), allowSceneObjects = false, tooltip = "The character who says the line" };
            characterField.AddToClassList("dialogue-node__character");
            characterField.RegisterValueChangedCallback(_event => SetCharacter(_event.newValue as DialogueCharacter));

            TextField = CreateTextField(() => NodeData.Text, () => NodeData.Id);

            VisualElement _content = new VisualElement();
            _content.AddToClassList("dialogue-node__content");
            _content.Add(characterField);
            _content.Add(TextField);
            extensionContainer.Insert(0, _content);
        }
        #endregion

        // ==================== METHODS ====================
        #region Methods
        internal override void Refresh() {
            base.Refresh();
            characterField.SetValueWithoutNotify(NodeData.Character);
            TextField.Reload();
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

        // ==================== PRIVATE ====================
        #region Private Methods
        private void SetCharacter(DialogueCharacter _character) {
            if (Asset == null || NodeData.Character == _character) return;
            Undo.RecordObject(Asset, CharacterUndo);
            NodeData.Character = _character;
            EditorUtility.SetDirty(Asset);
        }
        #endregion
    }
}
