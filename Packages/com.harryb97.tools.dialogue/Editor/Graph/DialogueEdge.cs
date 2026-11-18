using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Connection between an output and a node. Drawn dimmer when it goes back, and highlighted as part of the path a running dialogue took </summary>
    internal sealed class DialogueEdge : Edge {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string TraversedClass = "traversed";
        private const string BackClass = "back";
        private const float BackAlpha = 0.4f;
        private static readonly Color TraversedColor = new Color(1f, 0.75f, 0.25f, 1f);
        #endregion

        #region Public Variables
        /// <summary> The id of the output the edge starts from </summary>
        internal string OutputId => output?.userData as string;

        /// <summary> A running dialogue went through this connection </summary>
        internal bool Traversed {
            get => ClassListContains(TraversedClass);
            set {
                if (value == Traversed) return;
                EnableInClassList(TraversedClass, value);
                UpdateEdgeControl();
            }
        }

        /// <summary> The target node is left of the source node or at the same x, so the edge goes back in the reading order. Drawn dimmer; it means the same </summary>
        internal bool IsBack {
            get => ClassListContains(BackClass);
            set {
                if (value == IsBack) return;
                EnableInClassList(BackClass, value);
                UpdateEdgeControl();
            }
        }
        #endregion

        // ==================== DRAW ====================
        #region Draw
        // The base sets the colors from the ports, which a style cannot override, whenever the edge is updated, restyled or (un)selected
        public override bool UpdateEdgeControl() {
            bool _updated = base.UpdateEdgeControl();
            ApplyColors();
            return _updated;
        }

        protected override void OnCustomStyleResolved(ICustomStyle _style) {
            base.OnCustomStyleResolved(_style);
            ApplyColors();
        }

        public override void OnSelected() {
            base.OnSelected();
            ApplyColors();
        }

        public override void OnUnselected() {
            base.OnUnselected();
            ApplyColors();
        }

        private void ApplyColors() {
            if (selected || edgeControl == null) return;
            if (Traversed) {
                edgeControl.inputColor = TraversedColor;
                edgeControl.outputColor = TraversedColor;
                return;
            }
            // From the ports each time, so a back edge is never dimmed twice and a mark taken away leaves no color behind
            Color _input = input != null ? input.portColor : defaultColor;
            Color _output = output != null ? output.portColor : defaultColor;
            edgeControl.inputColor = IsBack ? Dim(_input) : _input;
            edgeControl.outputColor = IsBack ? Dim(_output) : _output;
        }

        private static Color Dim(Color _color) => new Color(_color.r, _color.g, _color.b, _color.a * BackAlpha);
        #endregion
    }
}
