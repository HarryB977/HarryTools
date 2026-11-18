using System;
using UnityEditor.Experimental.GraphView;
using UnityEngine.UIElements;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Multiline text field placed on a node. What is typed is written once, when the field loses focus; Escape puts back the stored text </summary>
    internal sealed class NodeTextField : TextField {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string FieldClass = "dialogue-node__text";

        private readonly Func<string> read;
        private readonly Action<string> write;
        private string stored = "";
        #endregion

        #region Public Variables
        /// <summary> The field holds text that has not been written yet </summary>
        internal bool HasPendingEdit => value != stored;
        #endregion

        // ==================== CONSTRUCTION ====================
        #region Construction
        internal NodeTextField(Func<string> _read, Action<string> _write) {
            read = _read;
            write = _write;
            multiline = true;
            // A click places the caret: selecting everything would let the next key replace the whole text
            selectAllOnFocus = false;
            selectAllOnMouseUp = false;
            AddToClassList(FieldClass);

            RegisterCallback<FocusOutEvent>(_ => Commit());
            // The canvas has its own keys (space opens the node search, letters frame the view) and commands (delete, copy, paste); while typing they belong to the text
            RegisterCallback<KeyDownEvent>(OnKeyDown);
            RegisterCallback<ValidateCommandEvent>(_event => _event.StopPropagation());
            RegisterCallback<ExecuteCommandEvent>(_event => _event.StopPropagation());
            // A click in the text selects the node but does not start dragging it or a connection
            RegisterCallback<MouseDownEvent>(OnMouseDown);
            RegisterCallback<PointerDownEvent>(_event => _event.StopPropagation());
            Reload();
        }
        #endregion

        // ==================== METHODS ====================
        #region Methods
        /// <summary> Writes the text when it changed since it was last read or written </summary>
        internal void Commit() {
            if (!HasPendingEdit) return;
            stored = value;
            write(stored);
        }

        /// <summary> Shows the stored text. Kept as is while the user has unwritten text in it </summary>
        internal void Reload() {
            if (HasPendingEdit) return;
            stored = read() ?? "";
            SetValueWithoutNotify(stored);
        }

        /// <summary> Drops unwritten text </summary>
        internal void Discard() {
            SetValueWithoutNotify(stored);
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        private void OnKeyDown(KeyDownEvent _event) {
            if (_event.keyCode == UnityEngine.KeyCode.Escape) {
                Discard();
                Blur();
            }
            _event.StopPropagation();
        }

        private void OnMouseDown(MouseDownEvent _event) {
            _event.StopPropagation();
            Node _node = GetFirstAncestorOfType<Node>();
            GraphView _graph = GetFirstAncestorOfType<GraphView>();
            if (_node == null || _graph == null || _node.selected) return;
            _graph.ClearSelection();
            _graph.AddToSelection(_node);
        }
        #endregion
    }
}
