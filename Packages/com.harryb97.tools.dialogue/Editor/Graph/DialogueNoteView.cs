using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Canvas view of a <see cref="DialogueNote"/>: free text with a size. Text and size edits are written to the asset; moves go through the graph </summary>
    internal sealed class DialogueNoteView : StickyNote {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string NoteClass = "dialogue-note";
        #endregion

        #region Public Variables
        internal string NoteId { get; }

        internal DialogueNote Data { get; }

        internal DialogueAsset Asset { get; }

        /// <summary> Where the note is placed on the canvas. Read from its style, which is up to date before the next layout pass </summary>
        internal Vector2 CanvasPosition => new Vector2(style.left.value.value, style.top.value.value);
        #endregion

        // ==================== CONSTRUCTION ====================
        #region Construction
        internal DialogueNoteView(DialogueAsset _asset, DialogueNote _note) : base(_note.Rect.position) {
            Asset = _asset;
            Data = _note;
            NoteId = _note.Id;
            viewDataKey = _note.Id;
            AddToClassList(NoteClass);

            // A note is only its text
            Label _title = this.Q<Label>("title");
            if (_title != null) _title.style.display = DisplayStyle.None;

            contents = _note.Text;
            SetPosition(_note.Rect);
            RegisterCallback<StickyNoteChangeEvent>(OnNoteChanged);
        }
        #endregion

        // ==================== METHODS ====================
        #region Methods
        public override void OnResized() {
            base.OnResized();
            Write(Data.Text, GetPosition());
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent _event) {
            // Theme and font size are not saved with the note, so they are not offered
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        private void OnNoteChanged(StickyNoteChangeEvent _event) {
            if (_event.change != StickyNoteChange.Contents || contents == Data.Text) return;
            Write(contents, Data.Rect);
        }

        private void Write(string _text, Rect _rect) {
            if (Asset == null) return;
            DialogueGraphView _graph = GetFirstAncestorOfType<DialogueGraphView>();
            if (_graph != null && !_graph.CanEdit) return;
            if (float.IsNaN(_rect.width) || float.IsNaN(_rect.height)) _rect = Data.Rect;
            DialogueGraphOps.SetNote(Asset, NoteId, _text, _rect);
        }
        #endregion
    }
}
