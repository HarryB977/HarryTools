using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Connection between an output and a node. Can be drawn as part of the path a running dialogue took </summary>
    internal sealed class DialogueEdge : Edge {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string TraversedClass = "traversed";
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
        #endregion

        // ==================== DRAW ====================
        #region Draw
        protected override void DrawEdge() {
            base.DrawEdge();
            // The base colors come from the ports, which a style cannot override
            if (!Traversed || selected || edgeControl == null) return;
            edgeControl.inputColor = TraversedColor;
            edgeControl.outputColor = TraversedColor;
        }
        #endregion
    }
}
