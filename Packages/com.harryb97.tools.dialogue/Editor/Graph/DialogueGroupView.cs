using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Canvas view of a <see cref="DialogueGroup"/>: a titled, colored box around its nodes that moves with them. Title and color edits are written to the asset </summary>
    internal sealed class DialogueGroupView : Group {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string GroupClass = "dialogue-group";
        private const string ColorClass = "dialogue-group__color";
        private const float BackgroundAlpha = 0.15f;
        private const float HeaderAlpha = 0.6f;

        private const string ColorUndo = "Group Color";

        private readonly ColorField colorField;
        // Undo group of the first change made with the color picker; its later changes are folded into it
        private int pickerUndoGroup = -1;
        #endregion

        #region Public Variables
        /// <summary> The colors offered in the group's menu. Any other color can be picked from the field in its header </summary>
        internal static readonly (string Name, Color Color)[] Palette = {
            ("Gray", new Color(0.45f, 0.45f, 0.45f, 1f)),
            ("Red", new Color(0.75f, 0.25f, 0.25f, 1f)),
            ("Orange", new Color(0.85f, 0.5f, 0.2f, 1f)),
            ("Yellow", new Color(0.8f, 0.7f, 0.2f, 1f)),
            ("Green", new Color(0.3f, 0.6f, 0.3f, 1f)),
            ("Teal", new Color(0.2f, 0.6f, 0.6f, 1f)),
            ("Blue", new Color(0.25f, 0.45f, 0.75f, 1f)),
            ("Purple", new Color(0.55f, 0.35f, 0.75f, 1f))
        };

        internal string GroupId { get; }

        /// <summary> The field in the header that opens the color picker </summary>
        internal ColorField ColorPicker => colorField;

        internal DialogueGroup Data { get; }

        internal DialogueAsset Asset { get; }
        #endregion

        // ==================== CONSTRUCTION ====================
        #region Construction
        internal DialogueGroupView(DialogueAsset _asset, DialogueGroup _group) {
            Asset = _asset;
            Data = _group;
            GroupId = _group.Id;
            viewDataKey = _group.Id;
            AddToClassList(GroupClass);
            title = _group.Title;

            colorField = new ColorField { showAlpha = false, showEyeDropper = false, value = _group.Color, tooltip = "Color of the group" };
            colorField.AddToClassList(ColorClass);
            colorField.RegisterValueChangedCallback(_event => PickColor(_event.newValue));
            // A click on the field opens the picker: a new interaction, so a new undo step
            colorField.RegisterCallback<PointerDownEvent>(_ => pickerUndoGroup = -1, TrickleDown.TrickleDown);
            headerContainer.Add(colorField);

            this.AddManipulator(new ContextualMenuManipulator(BuildColorMenu));
            ApplyColor(_group.Color);
        }
        #endregion

        // ==================== METHODS ====================
        #region Methods
        /// <summary> Writes the color to the asset and shows it </summary>
        internal void SetColor(Color _color) {
            if (!CanWrite) {
                colorField.SetValueWithoutNotify(Data.Color);
                return;
            }
            _color.a = 1f;
            colorField.SetValueWithoutNotify(_color);
            ApplyColor(_color);
            if (Asset == null || Data.Color == _color) return;
            DialogueGraphOps.SetGroup(Asset, GroupId, Data.Title, _color);
        }

        public override bool AcceptsElement(GraphElement _element, ref string _reasonWhyNotAccepted) {
            // Groups hold nodes only; notes stay free on the canvas
            if (!(_element is DialogueNodeView)) return false;
            return base.AcceptsElement(_element, ref _reasonWhyNotAccepted);
        }

        public override string title {
            get => base.title;
            set {
                base.title = value;
                WriteTitle(value);
            }
        }

        protected override void OnGroupRenamed(string _oldName, string _newName) {
            base.OnGroupRenamed(_oldName, _newName);
            WriteTitle(_newName);
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        private void WriteTitle(string _title) {
            if (Asset == null || Data == null || (_title ?? "") == (Data.Title ?? "")) return;
            if (!CanWrite) {
                // A rename the canvas does not allow shows the stored title again
                base.title = Data.Title;
                return;
            }
            DialogueGraphOps.SetGroup(Asset, GroupId, _title, Data.Color);
        }

        /// <summary> False while the canvas holding the group is read only </summary>
        private bool CanWrite {
            get {
                DialogueGraphView _graph = GetFirstAncestorOfType<DialogueGraphView>();
                return _graph == null || _graph.CanEdit;
            }
        }

        /// <summary> A change from the color picker. Dragging in the picker sends many, which make a single undo step </summary>
        private void PickColor(Color _color) {
            if (pickerUndoGroup < 0) {
                Undo.IncrementCurrentGroup();
                pickerUndoGroup = Undo.GetCurrentGroup();
            }
            SetColor(_color);
            Undo.SetCurrentGroupName(ColorUndo);
            Undo.CollapseUndoOperations(pickerUndoGroup);
        }

        private void ApplyColor(Color _color) {
            style.backgroundColor = new Color(_color.r, _color.g, _color.b, BackgroundAlpha);
            style.borderTopColor = _color;
            style.borderBottomColor = _color;
            style.borderLeftColor = _color;
            style.borderRightColor = _color;
            headerContainer.style.backgroundColor = new Color(_color.r, _color.g, _color.b, HeaderAlpha);
        }

        private void BuildColorMenu(ContextualMenuPopulateEvent _event) {
            for (int _i = 0; _i < Palette.Length; _i++) {
                Color _color = Palette[_i].Color;
                _event.menu.AppendAction("Color/" + Palette[_i].Name, _ => SetColor(_color), _ => SameColor(Data.Color, _color) ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            }
            _event.menu.AppendSeparator();
        }

        private static bool SameColor(Color _a, Color _b) {
            return Mathf.Approximately(_a.r, _b.r) && Mathf.Approximately(_a.g, _b.g) && Mathf.Approximately(_a.b, _b.b);
        }
        #endregion
    }
}
