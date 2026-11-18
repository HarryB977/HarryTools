using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Canvas of a dialogue. The asset is the only source of truth: every edit goes through <see cref="DialogueGraphOps"/> and the canvas is rebuilt from the asset </summary>
    internal sealed class DialogueGraphView : GraphView {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string CameraKeyPrefix = "HarryTools.Dialogue.Graph.Camera.";
        private const float GridSize = 20f;
        private const long FrameRetryMilliseconds = 50;
        // Tries of a delayed frame before it gives up on a node that never gets a size
        private const int MaxFrameRetries = 20;

        private const string DefaultGroupTitle = "Group";
        private const string DuplicateOperation = "Duplicate";
        private static readonly Vector2 PasteOffset = new Vector2(40f, 40f);
        private static readonly Vector2 NoteSize = new Vector2(200f, 100f);

        private readonly Dictionary<string, DialogueNodeView> nodeViews = new Dictionary<string, DialogueNodeView>();
        private readonly Dictionary<string, DialogueGroupView> groupViews = new Dictionary<string, DialogueGroupView>();
        private readonly Dictionary<string, DialogueNoteView> noteViews = new Dictionary<string, DialogueNoteView>();
        private readonly Dictionary<string, DialogueEdge> edgeViews = new Dictionary<string, DialogueEdge>();
        private readonly List<NodeOutput> outputBuffer = new List<NodeOutput>();
        private readonly EditorWindow window;
        private readonly EdgeListener edgeListener;
        private readonly MiniMap miniMap;
        private DialogueNodeSearch search;
        private bool restoringCamera;
        // Set while the canvas moves the camera on its own, after a layout pass
        private bool movingCamera;
        // Counts the camera requests, so a delayed one gives way to a newer one
        private int cameraRequest;
        private bool readOnly;
        private bool rebuilding;
        // Last canvas point under the mouse, where pasted elements land
        private Vector2 pastePosition;
        private bool hasPastePosition;
        // Checking clipboard data parses it, so the last answer is kept
        private string pasteCheckData;
        private bool pasteCheckResult;
        #endregion

        #region Public Variables
        /// <summary> Styles of the canvas, its nodes and the side panel </summary>
        internal const string StyleSheetPath = "Packages/com.harryb97.tools.dialogue/Editor/Graph/DialogueGraph.uss";

        internal DialogueAsset Asset { get; private set; }

        /// <summary> Raised when the selected elements change. Not raised in the middle of a rebuild, only once after it </summary>
        internal event Action SelectionChanged;

        /// <summary> Raised after the canvas was recreated from the asset </summary>
        internal event Action Rebuilt;

        /// <summary> The canvas has a dialogue and is not read only. Checked by every edit of the asset </summary>
        internal bool CanEdit => Asset != null && !ReadOnly;

        /// <summary> Raised when the camera moves other than by the canvas itself: by the user, or by a call to <see cref="Frame"/> </summary>
        internal event Action CameraMoved;

        /// <summary> Nothing on the canvas can be created, deleted, connected, moved or typed. Changing it rebuilds the canvas </summary>
        internal bool ReadOnly {
            get => readOnly;
            set {
                if (value == readOnly) return;
                readOnly = value;
                Rebuild();
            }
        }

        /// <summary> The id of the only selected node. Null when no node or more than one is selected </summary>
        internal string SelectedNodeId {
            get {
                string _id = null;
                foreach (ISelectable _item in selection) {
                    if (!(_item is DialogueNodeView _view)) continue;
                    if (_id != null) return null;
                    _id = _view.NodeId;
                }
                return _id;
            }
        }

        /// <summary> Moved nodes land on the background grid </summary>
        internal bool SnapToGrid { get; set; }

        internal bool MiniMapVisible {
            get => miniMap.parent == this;
            set {
                if (value == MiniMapVisible) return;
                if (value) Add(miniMap);
                else miniMap.RemoveFromHierarchy();
            }
        }
        #endregion

        // ==================== CONSTRUCTION ====================
        #region Construction
        internal DialogueGraphView(EditorWindow _window) {
            window = _window;
            StyleSheet _style = AssetDatabase.LoadAssetAtPath<StyleSheet>(StyleSheetPath);
            if (_style != null) styleSheets.Add(_style);
            AddToClassList("dialogue-graph");

            GridBackground _grid = new GridBackground();
            Insert(0, _grid);
            _grid.StretchToParentSize();

            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());

            miniMap = new MiniMap { anchored = true };
            miniMap.SetPosition(new Rect(8f, 8f, 200f, 140f));

            edgeListener = new EdgeListener(this);
            // Requested by the graph view on the space bar
            nodeCreationRequest = _context => OpenSearch(_context.screenMousePosition, null);
            graphViewChanged = OnGraphViewChanged;
            viewTransformChanged = _ => {
                SaveCamera();
                if (restoringCamera || movingCamera) return;
                // A camera moved from outside wins over a delayed frame still waiting for the layout
                cameraRequest++;
                CameraMoved?.Invoke();
            };
            serializeGraphElements = SerializeElements;
            canPasteSerializedData = CanPasteData;
            unserializeAndPaste = PasteData;
            elementsAddedToGroup = (_group, _) => OnGroupMembersChanged(_group);
            elementsRemovedFromGroup = (_group, _) => OnGroupMembersChanged(_group);

            RegisterCallback<KeyDownEvent>(OnKeyDown);
            RegisterCallback<MouseMoveEvent>(_event => RememberPastePosition(_event.mousePosition));

            RegisterCallback<AttachToPanelEvent>(_ => DialogueTables.SourceTextWritten += OnSourceTextWritten);
            RegisterCallback<DetachFromPanelEvent>(_ => {
                DialogueTables.SourceTextWritten -= OnSourceTextWritten;
                DestroySearch();
            });
        }
        #endregion

        // ==================== ASSET ====================
        #region Asset
        /// <summary> Shows the asset, with the camera it had last time in this editor session. Null leaves the canvas empty </summary>
        internal void Load(DialogueAsset _asset) {
            Asset = _asset;
            hasPastePosition = false;
            // Dialogues made without the window have every node at the origin
            if (CanEdit && DialogueGraphOps.NeedsAutoLayout(Asset)) {
                Undo.IncrementCurrentGroup();
                DialogueGraphOps.AutoLayout(Asset);
                Undo.IncrementCurrentGroup();
            }
            Rebuild();
            RestoreCamera();
        }

        /// <summary> Recreates every node, edge, group and note from the asset, keeping the camera and the selection </summary>
        internal void Rebuild() {
            HashSet<string> _selected = new HashSet<string>();
            foreach (ISelectable _item in selection) {
                string _id = ElementId(_item);
                if (_id != null) _selected.Add(_id);
            }
            RebuildSelecting(_selected);
        }

        /// <summary> Recreates the canvas from the asset and selects the elements with these ids </summary>
        private void RebuildSelecting(HashSet<string> _selected) {
            rebuilding = true;
            try {
                RebuildElements(_selected);
            } finally {
                rebuilding = false;
            }
            SelectionChanged?.Invoke();
            Rebuilt?.Invoke();
        }

        private void RebuildElements(HashSet<string> _selected) {
            // Text still being typed is carried over to the new views unwritten: writing it now would land inside an undo or redo
            Dictionary<string, string> _pending = new Dictionary<string, string>();
            string _focusedKey = null;
            this.Query<NodeTextField>().ForEach(_field => {
                if (_field.Key == null || !_field.HasPendingEdit) return;
                _pending[_field.Key] = _field.value;
                if (_field.focusController?.focusedElement == _field) _focusedKey = _field.Key;
                _field.Discard();
            });

            RebuildViews(_selected);

            // A read only canvas cannot write it, so it is dropped
            if (_pending.Count == 0 || ReadOnly) return;
            // A text whose node or option is gone after the rebuild is dropped
            this.Query<NodeTextField>().ForEach(_field => {
                if (_field.Key == null || !_pending.TryGetValue(_field.Key, out string _text)) return;
                _field.RestorePending(_text);
                if (_field.Key == _focusedKey) _field.Focus();
            });
        }

        private void RebuildViews(HashSet<string> _selected) {

            ClearSelection();
            foreach (GraphElement _element in graphElements.ToList()) RemoveElement(_element);
            nodeViews.Clear();
            groupViews.Clear();
            noteViews.Clear();
            edgeViews.Clear();

            if (Asset == null) return;

            for (int _i = 0; _i < Asset.Nodes.Count; _i++) {
                DialogueNode _node = Asset.Nodes[_i];
                // A node whose class no longer exists deserializes as null; the validator reports it
                if (_node == null || string.IsNullOrEmpty(_node.Id) || nodeViews.ContainsKey(_node.Id)) continue;
                // Ports without a listener cannot start connections
                DialogueNodeView _view = DialogueNodeView.Create(Asset, _node, ReadOnly ? null : edgeListener);
                if (ReadOnly) _view.MakeReadOnly();
                nodeViews.Add(_node.Id, _view);
                AddElement(_view);
            }

            foreach (DialogueNodeView _view in nodeViews.Values) {
                outputBuffer.Clear();
                _view.Data.CollectOutputs(outputBuffer);
                for (int _o = 0; _o < outputBuffer.Count; _o++) {
                    NodeOutput _output = outputBuffer[_o];
                    Port _port = _output != null && _output.IsConnected ? _view.FindOutputPort(_output.Id) : null;
                    if (_port == null) continue;
                    if (!nodeViews.TryGetValue(_output.Target, out DialogueNodeView _target) || _target.Input == null) continue;
                    DialogueEdge _edge = _port.ConnectTo<DialogueEdge>(_target.Input);
                    if (ReadOnly) _edge.capabilities &= ~Capabilities.Deletable;
                    edgeViews[_output.Id] = _edge;
                    AddElement(_edge);
                }
            }

            for (int _i = 0; _i < Asset.Groups.Count; _i++) {
                DialogueGroup _group = Asset.Groups[_i];
                if (_group == null || string.IsNullOrEmpty(_group.Id) || groupViews.ContainsKey(_group.Id)) continue;
                DialogueGroupView _view = new DialogueGroupView(Asset, _group);
                if (ReadOnly) {
                    _view.capabilities &= ~(Capabilities.Movable | Capabilities.Deletable | Capabilities.Renamable);
                    _view.ColorPicker.SetEnabled(false);
                }
                groupViews.Add(_group.Id, _view);
                AddElement(_view);
                for (int _n = 0; _n < _group.NodeIds.Count; _n++) {
                    if (nodeViews.TryGetValue(_group.NodeIds[_n], out DialogueNodeView _member)) _view.AddElement(_member);
                }
            }

            for (int _i = 0; _i < Asset.Notes.Count; _i++) {
                DialogueNote _note = Asset.Notes[_i];
                if (_note == null || string.IsNullOrEmpty(_note.Id) || noteViews.ContainsKey(_note.Id)) continue;
                DialogueNoteView _view = new DialogueNoteView(Asset, _note);
                if (ReadOnly) {
                    _view.capabilities &= ~(Capabilities.Movable | Capabilities.Deletable | Capabilities.Resizable | Capabilities.Renamable);
                    _view.Query<TextField>().ForEach(_field => _field.isReadOnly = true);
                }
                noteViews.Add(_note.Id, _view);
                AddElement(_view);
            }

            foreach (string _id in _selected) {
                GraphElement _element = FindElement(_id);
                if (_element != null) AddToSelection(_element);
            }
        }

        /// <summary> Updates the node's view after its data changed. The graph is rebuilt when the node's outputs changed </summary>
        internal void RefreshNode(string _id) {
            DialogueNodeView _view = FindNodeView(_id);
            if (_view == null) return;
            if (_view.OutputsChanged()) Rebuild();
            else _view.Refresh();
        }

        /// <summary> Writes the text typed on the nodes that has not been written yet </summary>
        internal void CommitPendingText() {
            this.Query<NodeTextField>().ForEach(_field => _field.Commit());
        }

        internal DialogueNodeView FindNodeView(string _id) {
            if (string.IsNullOrEmpty(_id)) return null;
            return nodeViews.TryGetValue(_id, out DialogueNodeView _view) ? _view : null;
        }

        /// <summary> The edge drawn for the connected output with the id. Null when it has none </summary>
        internal DialogueEdge FindEdge(string _outputId) {
            if (string.IsNullOrEmpty(_outputId)) return null;
            return edgeViews.TryGetValue(_outputId, out DialogueEdge _edge) ? _edge : null;
        }

        /// <summary> Centers the camera on the node, keeping the zoom. A node not laid out yet is centered again once it has a size </summary>
        internal void Frame(string _nodeId) {
            DialogueNodeView _view = FindNodeView(_nodeId);
            if (_view == null) return;

            CenterOn(_view);
            // After centering, which counts as a camera move from outside
            int _request = ++cameraRequest;
            if (IsLaidOut(_view)) return;
            bool _done = false;
            int _tries = 0;
            schedule.Execute(() => {
                if (_request != cameraRequest || _view.parent == null || ++_tries > MaxFrameRetries) {
                    _done = true;
                    return;
                }
                if (!IsLaidOut(_view)) return;
                movingCamera = true;
                try {
                    CenterOn(_view);
                } finally {
                    movingCamera = false;
                }
                _done = true;
            }).Every(FrameRetryMilliseconds).Until(() => _done);
        }

        private static bool IsLaidOut(VisualElement _element) => !float.IsNaN(_element.layout.width) && _element.layout.width > 0f;

        private void CenterOn(DialogueNodeView _view) {
            Vector2 _size = new Vector2(float.IsNaN(_view.layout.width) ? 0f : _view.layout.width, float.IsNaN(_view.layout.height) ? 0f : _view.layout.height);
            Vector2 _center = _view.CanvasPosition + _size * 0.5f;
            float _scale = CameraScale;
            Vector2 _viewport = new Vector2(float.IsNaN(layout.width) ? 0f : layout.width, float.IsNaN(layout.height) ? 0f : layout.height);
            UpdateViewTransform(_viewport * 0.5f - _center * _scale, new Vector3(_scale, _scale, 1f));
        }
        #endregion

        // ==================== EDITS ====================
        #region Edits
        /// <summary> Adds a node at the canvas position; when an output id is given, that output points to it </summary>
        internal DialogueNode CreateNode(Type _type, Vector2 _position, string _fromOutputId) {
            if (!CanEdit) return null;
            if (SnapToGrid) _position = Snap(_position);

            DialogueNode _node = string.IsNullOrEmpty(_fromOutputId)
                ? DialogueGraphOps.CreateNode(Asset, _type, _position)
                : DialogueGraphOps.CreateConnected(Asset, _type, _position, _fromOutputId);
            Rebuild();
            return _node;
        }

        internal void Connect(string _outputId, string _targetNodeId) {
            if (!CanEdit) return;
            DialogueGraphOps.Connect(Asset, _outputId, _targetNodeId);
            Rebuild();
        }

        /// <summary> Opens the node type search at the screen position. The created node is connected to the output when one is given </summary>
        internal void OpenSearch(Vector2 _screenPosition, string _fromOutputId) {
            if (!CanEdit) return;
            if (search == null) {
                search = ScriptableObject.CreateInstance<DialogueNodeSearch>();
                search.hideFlags = HideFlags.HideAndDontSave;
            }
            search.Setup(this, _fromOutputId);
            SearchWindow.Open(new SearchWindowContext(_screenPosition), search);
        }

        /// <summary> The canvas position under a screen position </summary>
        internal Vector2 ScreenToCanvas(Vector2 _screenPosition) {
            // The window's content starts below its tab, which is part of the panel
            Vector2 _panel = window.rootVisualElement.LocalToWorld(_screenPosition - window.position.position);
            return contentViewContainer.WorldToLocal(_panel);
        }

        /// <summary> The screen position of a point of the window's panel </summary>
        private Vector2 PanelToScreen(Vector2 _panelPosition) {
            return window.rootVisualElement.WorldToLocal(_panelPosition) + window.position.position;
        }

        /// <summary> Selects only the node and centers the camera on it </summary>
        internal void SelectAndFrame(string _nodeId) {
            DialogueNodeView _view = FindNodeView(_nodeId);
            if (_view == null) return;
            ClearSelection();
            AddToSelection(_view);
            Frame(_nodeId);
        }

        /// <summary> Lays every node out again by its distance from the start node </summary>
        internal void AutoLayout() {
            if (!CanEdit) return;
            DialogueGraphOps.AutoLayout(Asset);
            Rebuild();
            FrameAllWhenLaidOut();
        }
        #endregion

        // ==================== GROUPS AND NOTES ====================
        #region Groups and Notes
        internal DialogueGroupView FindGroupView(string _id) {
            if (string.IsNullOrEmpty(_id)) return null;
            return groupViews.TryGetValue(_id, out DialogueGroupView _view) ? _view : null;
        }

        internal DialogueNoteView FindNoteView(string _id) {
            if (string.IsNullOrEmpty(_id)) return null;
            return noteViews.TryGetValue(_id, out DialogueNoteView _view) ? _view : null;
        }

        /// <summary> Puts the selected nodes in a new group, which is then selected </summary>
        internal void GroupSelection() {
            List<string> _ids = SelectedNodeIds();
            if (!CanEdit || _ids.Count == 0) return;

            DialogueGroup _group = DialogueGraphOps.CreateGroup(Asset, DefaultGroupTitle, _ids);
            Undo.SetCurrentGroupName("Create Group");
            RebuildSelecting(new HashSet<string> { _group.Id });
        }

        /// <summary> Takes the selected nodes out of their groups </summary>
        internal void RemoveSelectionFromGroups() {
            if (!CanEdit) return;
            List<DialogueNodeView> _nodes = SelectedNodes();
            foreach (DialogueGroupView _group in groupViews.Values.ToList()) {
                List<GraphElement> _members = new List<GraphElement>();
                for (int _i = 0; _i < _nodes.Count; _i++) {
                    if (_group.ContainsElement(_nodes[_i])) _members.Add(_nodes[_i]);
                }
                // Raises elementsRemovedFromGroup, which writes the asset
                if (_members.Count > 0) _group.RemoveElements(_members);
            }
            Undo.SetCurrentGroupName("Remove from Group");
        }

        /// <summary> Adds a note at the canvas position, which is then selected </summary>
        internal DialogueNote CreateNote(Vector2 _position) {
            if (!CanEdit) return null;
            if (SnapToGrid) _position = Snap(_position);

            DialogueNote _note = DialogueGraphOps.CreateNote(Asset, new Rect(_position, NoteSize));
            Undo.SetCurrentGroupName("Create Note");
            RebuildSelecting(new HashSet<string> { _note.Id });
            return _note;
        }

        private void OnGroupMembersChanged(Group _group) {
            if (rebuilding || !CanEdit || !(_group is DialogueGroupView _view)) return;

            List<string> _ids = new List<string>();
            foreach (GraphElement _element in _view.containedElements) {
                if (_element is DialogueNodeView _node) _ids.Add(_node.NodeId);
            }
            if (_ids.Count > 0) {
                DialogueGraphOps.SetGroupNodes(Asset, _view.GroupId, _ids);
                return;
            }

            // A group is drawn around its nodes, so one left without nodes goes away. Rebuilt later: the graph view is still moving elements
            DialogueGraphOps.Delete(Asset, new[] { _view.GroupId });
            schedule.Execute(() => {
                if (Asset != null) Rebuild();
            });
        }
        #endregion

        // ==================== CLIPBOARD ====================
        #region Clipboard
        private string testClipboard;

        /// <summary> Copies the selection to the system clipboard </summary>
        internal void CopySelection() {
            if (testClipboard != null) {
                testClipboard = SerializeElements(selection.OfType<GraphElement>());
                return;
            }
            CopySelectionCallback();
        }

        /// <summary> Makes copy and paste use a private buffer instead of the system clipboard, which an unfocused Editor cannot read. Only for tests </summary>
        internal void UseTestClipboard() {
            testClipboard = "";
        }

        /// <summary> Adds what the system clipboard holds under the mouse, or next to where it was copied from </summary>
        internal void Paste() {
            if (testClipboard != null) {
                if (CanPasteData(testClipboard)) PasteData("Paste", testClipboard);
                return;
            }
            PasteCallback();
        }

        internal void DuplicateSelection() {
            DuplicateSelectionCallback();
        }

        private string SerializeElements(IEnumerable<GraphElement> _elements) {
            if (Asset == null) return "";
            CommitPendingText();

            HashSet<string> _ids = new HashSet<string>();
            foreach (GraphElement _element in _elements) {
                string _id = ElementId(_element);
                if (_id != null) _ids.Add(_id);
                // Copying a group copies its nodes
                if (!(_element is DialogueGroupView _group)) continue;
                foreach (GraphElement _member in _group.containedElements) {
                    if (_member is DialogueNodeView _node) _ids.Add(_node.NodeId);
                }
            }
            return DialogueClipboard.Copy(Asset, _ids);
        }

        private bool CanPasteData(string _data) {
            if (_data != pasteCheckData) {
                pasteCheckData = _data;
                pasteCheckResult = DialogueClipboard.CanPaste(_data);
            }
            return CanEdit && pasteCheckResult;
        }

        private void PasteData(string _operation, string _data) {
            // Duplicate reaches here without asking canPasteSerializedData
            if (!CanEdit) return;
            CommitPendingText();

            IReadOnlyList<string> _ids = DialogueClipboard.Paste(Asset, _data, Vector2.zero);
            if (_ids.Count == 0) return;

            // Pasted elements land under the mouse; duplicates and pastes without a mouse position land next to the originals
            bool _duplicate = _operation == DuplicateOperation;
            Dictionary<string, Vector2> _positions = PastedPositions(_ids);
            Vector2 _offset = PasteOffset;
            if (!_duplicate && hasPastePosition && _positions.Count > 0) {
                Vector2 _min = new Vector2(float.MaxValue, float.MaxValue);
                foreach (Vector2 _position in _positions.Values) _min = Vector2.Min(_min, _position);
                _offset = pastePosition - _min;
            }
            if (SnapToGrid) _offset = Snap(_offset);

            List<string> _moved = _positions.Keys.ToList();
            foreach (string _id in _moved) _positions[_id] += _offset;
            DialogueGraphOps.Move(Asset, _positions);
            Undo.SetCurrentGroupName(_duplicate ? "Duplicate" : "Paste");

            RebuildSelecting(new HashSet<string>(_ids));
        }

        /// <summary> Current positions of the pasted nodes and notes </summary>
        private Dictionary<string, Vector2> PastedPositions(IReadOnlyList<string> _ids) {
            Dictionary<string, Vector2> _positions = new Dictionary<string, Vector2>();
            HashSet<string> _pasted = new HashSet<string>(_ids);
            for (int _i = 0; _i < Asset.Nodes.Count; _i++) {
                DialogueNode _node = Asset.Nodes[_i];
                if (_node != null && _pasted.Contains(_node.Id)) _positions[_node.Id] = _node.Position;
            }
            for (int _i = 0; _i < Asset.Notes.Count; _i++) {
                DialogueNote _note = Asset.Notes[_i];
                if (_note != null && _pasted.Contains(_note.Id)) _positions[_note.Id] = _note.Rect.position;
            }
            return _positions;
        }

        private void RememberPastePosition(Vector2 _panelPosition) {
            pastePosition = contentViewContainer.WorldToLocal(_panelPosition);
            hasPastePosition = true;
        }
        #endregion

        // ==================== ARRANGE ====================
        #region Arrange
        /// <summary> Aligns or distributes the selected nodes </summary>
        internal void ArrangeSelection(DialogueGraphOps.Arrangement _arrangement) {
            if (!CanEdit) return;

            Dictionary<string, Rect> _rects = new Dictionary<string, Rect>();
            List<DialogueNodeView> _nodes = SelectedNodes();
            for (int _i = 0; _i < _nodes.Count; _i++) {
                Vector2 _size = new Vector2(float.IsNaN(_nodes[_i].layout.width) ? 0f : _nodes[_i].layout.width, float.IsNaN(_nodes[_i].layout.height) ? 0f : _nodes[_i].layout.height);
                _rects[_nodes[_i].NodeId] = new Rect(_nodes[_i].CanvasPosition, _size);
            }
            if (_rects.Count < 2) return;

            Dictionary<string, Vector2> _positions = new Dictionary<string, Vector2>(DialogueGraphOps.Arrange(_rects, _arrangement));
            if (SnapToGrid) {
                foreach (string _id in _positions.Keys.ToList()) _positions[_id] = Snap(_positions[_id]);
            }
            DialogueGraphOps.Move(Asset, _positions);
            string _name = ObjectNames.NicifyVariableName(_arrangement.ToString());
            Undo.SetCurrentGroupName(_name.StartsWith("Distribute") ? _name : "Align " + _name);
            Rebuild();
        }
        #endregion

        // ==================== MENU ====================
        #region Menu
        /// <summary> The menu of a right click on the empty canvas, at that panel position </summary>
        internal void BuildCanvasMenu(DropdownMenu _menu, Vector2 _panelPosition) {
            RememberPastePosition(_panelPosition);
            Vector2 _canvas = pastePosition;
            Vector2 _screen = PanelToScreen(_panelPosition);
            DropdownMenuAction.Status Available(bool _available) => _available && CanEdit ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled;

            _menu.AppendAction("Create Node", _ => OpenSearch(_screen, null), _ => Available(Asset != null));
            _menu.AppendSeparator();
            _menu.AppendAction("Paste", _ => Paste(), _ => Available(CanPasteData(EditorGUIUtility.systemCopyBuffer)));
            _menu.AppendAction("Duplicate", _ => DuplicateSelection(), _ => Available(Asset != null && HasCopyableSelection()));
            _menu.AppendSeparator();
            _menu.AppendAction("Auto Layout", _ => AutoLayout(), _ => Available(Asset != null));
            _menu.AppendAction("Create Group", _ => GroupSelection(), _ => Available(Asset != null && SelectedNodeIds().Count > 0));
            _menu.AppendAction("Create Note", _ => CreateNote(_canvas), _ => Available(Asset != null));
        }

        /// <summary> Items added after the graph view's own (cut, copy, paste, delete, duplicate) when an element is right clicked </summary>
        internal void BuildElementMenu(DropdownMenu _menu, VisualElement _target) {
            // The graph view's menu leaves notes out, so a note gets its edit items here
            DialogueNoteView _note = _target?.GetFirstOfType<DialogueNoteView>();
            if (_note != null) {
                if (!selection.Contains(_note)) {
                    ClearSelection();
                    AddToSelection(_note);
                }
                AppendMissing(_menu, "Copy", CopySelection, HasCopyableSelection());
                AppendMissing(_menu, "Duplicate", DuplicateSelection, CanEdit && HasCopyableSelection());
                AppendMissing(_menu, "Delete", () => DeleteSelection(), CanEdit && HasDeletableSelection());
            }
            BuildSelectionMenu(_menu);
        }

        private static void AppendMissing(DropdownMenu _menu, string _name, Action _action, bool _enabled) {
            if (_menu.MenuItems().OfType<DropdownMenuAction>().Any(_item => _item.name == _name)) return;
            _menu.AppendAction(_name, _ => _action(), _enabled ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
        }

        private void BuildSelectionMenu(DropdownMenu _menu) {
            List<DialogueNodeView> _nodes = SelectedNodes();
            if (_nodes.Count == 0 || !CanEdit) return;

            _menu.AppendSeparator();
            _menu.AppendAction("Create Group", _ => GroupSelection());
            bool _grouped = false;
            foreach (DialogueGroupView _group in groupViews.Values) {
                for (int _i = 0; _i < _nodes.Count && !_grouped; _i++) _grouped = _group.ContainsElement(_nodes[_i]);
            }
            if (_grouped) _menu.AppendAction("Remove from Group", _ => RemoveSelectionFromGroups());
            if (_nodes.Count < 2) return;

            _menu.AppendSeparator();
            AppendArrange(_menu, "Align/Left", DialogueGraphOps.Arrangement.Left, true);
            AppendArrange(_menu, "Align/Center", DialogueGraphOps.Arrangement.Center, true);
            AppendArrange(_menu, "Align/Right", DialogueGraphOps.Arrangement.Right, true);
            AppendArrange(_menu, "Align/Top", DialogueGraphOps.Arrangement.Top, true);
            AppendArrange(_menu, "Align/Middle", DialogueGraphOps.Arrangement.Middle, true);
            AppendArrange(_menu, "Align/Bottom", DialogueGraphOps.Arrangement.Bottom, true);
            AppendArrange(_menu, "Distribute/Horizontally", DialogueGraphOps.Arrangement.DistributeHorizontally, _nodes.Count > 2);
            AppendArrange(_menu, "Distribute/Vertically", DialogueGraphOps.Arrangement.DistributeVertically, _nodes.Count > 2);
        }

        private void AppendArrange(DropdownMenu _menu, string _name, DialogueGraphOps.Arrangement _arrangement, bool _enabled) {
            _menu.AppendAction(_name, _ => ArrangeSelection(_arrangement), _enabled ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
        }

        private void OnKeyDown(KeyDownEvent _event) {
            if (!_event.actionKey || _event.shiftKey || _event.altKey || _event.keyCode != KeyCode.G || IsTextInput(_event.target)) return;
            GroupSelection();
            _event.StopPropagation();
        }
        #endregion

        // ==================== SELECTION ====================
        #region Selection
        private List<DialogueNodeView> SelectedNodes() {
            List<DialogueNodeView> _nodes = new List<DialogueNodeView>();
            foreach (ISelectable _item in selection) {
                if (_item is DialogueNodeView _view) _nodes.Add(_view);
            }
            return _nodes;
        }

        private List<string> SelectedNodeIds() {
            return SelectedNodes().Select(_view => _view.NodeId).ToList();
        }

        private bool HasDeletableSelection() {
            foreach (ISelectable _item in selection) {
                if (_item is GraphElement _element && (_element.capabilities & Capabilities.Deletable) != 0) return true;
            }
            return false;
        }

        /// <summary> The event comes from a field the user types in, where shortcuts belong to the text </summary>
        internal static bool IsTextInput(IEventHandler _target) {
            return _target is VisualElement _element && _element.GetFirstOfType<TextField>() != null;
        }

        private bool HasCopyableSelection() {
            foreach (ISelectable _item in selection) {
                if (_item is GraphElement _element && _element.IsCopiable()) return true;
            }
            return false;
        }

        /// <summary> The id of the node, group or note the element shows. Null for anything else </summary>
        private static string ElementId(ISelectable _item) {
            switch (_item) {
                case DialogueNodeView _node: return _node.NodeId;
                case DialogueGroupView _group: return _group.GroupId;
                case DialogueNoteView _note: return _note.NoteId;
                default: return null;
            }
        }

        private GraphElement FindElement(string _id) {
            if (string.IsNullOrEmpty(_id)) return null;
            if (nodeViews.TryGetValue(_id, out DialogueNodeView _node)) return _node;
            if (groupViews.TryGetValue(_id, out DialogueGroupView _group)) return _group;
            return noteViews.TryGetValue(_id, out DialogueNoteView _note) ? _note : null;
        }
        #endregion

        // ==================== GRAPH VIEW ====================
        #region Graph View
        public override List<Port> GetCompatiblePorts(Port _startPort, NodeAdapter _nodeAdapter) {
            List<Port> _compatible = new List<Port>();
            // Cycles are allowed, including a node that leads back to itself
            foreach (Port _port in ports) {
                if (_port.direction != _startPort.direction) _compatible.Add(_port);
            }
            return _compatible;
        }

        public override void AddToSelection(ISelectable _selectable) {
            base.AddToSelection(_selectable);
            NotifySelection();
        }

        public override void RemoveFromSelection(ISelectable _selectable) {
            base.RemoveFromSelection(_selectable);
            NotifySelection();
        }

        public override void ClearSelection() {
            base.ClearSelection();
            NotifySelection();
        }

        private void NotifySelection() {
            if (!rebuilding) SelectionChanged?.Invoke();
        }

        private void OnSourceTextWritten(DialogueAsset _asset) {
            if (_asset != Asset) return;
            foreach (DialogueNodeView _view in nodeViews.Values) _view.Refresh();
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent _event) {
            // The empty canvas gets its own menu, led by the node search
            if (_event.target == this) {
                BuildCanvasMenu(_event.menu, _event.mousePosition);
                return;
            }
            base.BuildContextualMenu(_event);
            BuildElementMenu(_event.menu, _event.target as VisualElement);
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange _change) {
            if (Asset == null) return _change;
            if (!CanEdit) return BlockChange(_change);

            if (_change.movedElements != null && _change.movedElements.Count > 0) ApplyMoves(_change.movedElements);

            bool _rebuild = false;
            if (_change.elementsToRemove != null && _change.elementsToRemove.Count > 0) {
                ApplyRemovals(_change.elementsToRemove);
                _change.elementsToRemove.Clear();
                _rebuild = true;
            }
            if (_change.edgesToCreate != null && _change.edgesToCreate.Count > 0) {
                for (int _i = 0; _i < _change.edgesToCreate.Count; _i++) ApplyEdge(_change.edgesToCreate[_i]);
                _change.edgesToCreate.Clear();
                _rebuild = true;
            }

            // The canvas is redrawn from the asset, so the graph view has nothing left to remove or connect itself
            if (_rebuild) Rebuild();
            return _change;
        }

        /// <summary> Drops what the graph view was about to change. The canvas is drawn again from the asset, so an element moved or an edge dragged off its port goes back </summary>
        private GraphViewChange BlockChange(GraphViewChange _change) {
            bool _any = (_change.movedElements != null && _change.movedElements.Count > 0)
                || (_change.elementsToRemove != null && _change.elementsToRemove.Count > 0)
                || (_change.edgesToCreate != null && _change.edgesToCreate.Count > 0);
            _change.elementsToRemove?.Clear();
            _change.edgesToCreate?.Clear();
            if (_any) schedule.Execute(() => Rebuild());
            return _change;
        }

        private void ApplyMoves(List<GraphElement> _moved) {
            Dictionary<string, Vector2> _positions = new Dictionary<string, Vector2>();
            void AddNode(DialogueNodeView _view) {
                if (_positions.ContainsKey(_view.NodeId)) return;
                Vector2 _position = _view.CanvasPosition;
                if (SnapToGrid) {
                    _position = Snap(_position);
                    _view.SetPosition(new Rect(_position, Vector2.zero));
                }
                _positions[_view.NodeId] = _position;
            }

            for (int _i = 0; _i < _moved.Count; _i++) {
                switch (_moved[_i]) {
                    case DialogueNodeView _node:
                        AddNode(_node);
                        break;
                    case DialogueGroupView _group:
                        // A moved group carries its nodes
                        foreach (GraphElement _member in _group.containedElements) {
                            if (_member is DialogueNodeView _memberNode) AddNode(_memberNode);
                        }
                        break;
                    case DialogueNoteView _note:
                        _positions[_note.NoteId] = _note.CanvasPosition;
                        break;
                }
            }
            DialogueGraphOps.Move(Asset, _positions);
        }

        private void ApplyRemovals(List<GraphElement> _removed) {
            List<string> _ids = new List<string>();
            List<string> _disconnected = new List<string>();
            for (int _i = 0; _i < _removed.Count; _i++) {
                string _id = ElementId(_removed[_i]);
                if (_id != null) _ids.Add(_id);
                else if (_removed[_i] is Edge _edge && _edge.output != null && _edge.output.userData is string _outputId) _disconnected.Add(_outputId);
            }

            // Groups whose nodes are all deleted go with them
            HashSet<string> _deleted = new HashSet<string>(_ids);
            for (int _g = 0; _g < Asset.Groups.Count; _g++) {
                DialogueGroup _group = Asset.Groups[_g];
                if (_group == null || _deleted.Contains(_group.Id) || _group.NodeIds.Count == 0) continue;
                if (_group.NodeIds.All(_member => _deleted.Contains(_member) && !(Asset.Find(_member) is StartNode))) _ids.Add(_group.Id);
            }

            for (int _i = 0; _i < _disconnected.Count; _i++) DialogueGraphOps.Disconnect(Asset, _disconnected[_i]);
            DialogueGraphOps.Delete(Asset, _ids);
            // One undo step named after what the user did
            Undo.SetCurrentGroupName(_ids.Count > 0 ? "Delete" : "Disconnect");
        }

        private void ApplyEdge(Edge _edge) {
            if (_edge.output == null || _edge.input == null) return;
            if (!(_edge.output.userData is string _outputId) || !(_edge.input.node is DialogueNodeView _target)) return;
            DialogueGraphOps.Connect(Asset, _outputId, _target.NodeId);
        }

        private static Vector2 Snap(Vector2 _position) {
            return new Vector2(Mathf.Round(_position.x / GridSize) * GridSize, Mathf.Round(_position.y / GridSize) * GridSize);
        }
        #endregion

        // ==================== CAMERA ====================
        #region Camera
        private string CameraKey => CameraKeyPrefix + Asset.AssetId;

#pragma warning disable CS0618 // GraphView keeps its camera in the obsolete ITransform; UpdateViewTransform writes it
        private Vector2 CameraPosition => viewTransform.position;

        private float CameraScale => viewTransform.scale.x;
#pragma warning restore CS0618

        private void SaveCamera() {
            if (Asset == null || restoringCamera) return;
            SessionState.SetVector3(CameraKey, new Vector3(CameraPosition.x, CameraPosition.y, CameraScale));
        }

        private void RestoreCamera() {
            if (Asset == null) return;

            // A negative zoom marks a dialogue never opened in this session
            Vector3 _camera = SessionState.GetVector3(CameraKey, new Vector3(0f, 0f, -1f));
            bool _stored = _camera.z > 0f;
            restoringCamera = true;
            UpdateViewTransform(_stored ? new Vector3(_camera.x, _camera.y, 0f) : Vector3.zero, _stored ? new Vector3(_camera.z, _camera.z, 1f) : Vector3.one);
            restoringCamera = false;
            if (_stored) return;

            FrameAllWhenLaidOut();
        }

        /// <summary> Frames every element once the nodes have a size, known after the next layout pass </summary>
        private void FrameAllWhenLaidOut() {
            DialogueAsset _framed = Asset;
            int _request = ++cameraRequest;
            bool _done = false;
            schedule.Execute(() => {
                if (Asset == null || Asset != _framed || _request != cameraRequest) {
                    _done = true;
                    return;
                }
                foreach (DialogueNodeView _view in nodeViews.Values) {
                    if (float.IsNaN(_view.layout.width) || _view.layout.width <= 0f) return;
                }
                movingCamera = true;
                try {
                    FrameAll();
                } finally {
                    movingCamera = false;
                }
                _done = true;
            }).Every(FrameRetryMilliseconds).Until(() => _done);
        }
        #endregion

        // ==================== SEARCH ====================
        #region Search
        private void DestroySearch() {
            if (search != null) UnityEngine.Object.DestroyImmediate(search);
            search = null;
        }

        /// <summary> Turns connections dropped by the user into edits of the asset </summary>
        private sealed class EdgeListener : IEdgeConnectorListener {
            private readonly DialogueGraphView view;

            internal EdgeListener(DialogueGraphView _view) {
                view = _view;
            }

            public void OnDropOutsidePort(Edge _edge, Vector2 _position) {
                // Only a connection dragged out of an output creates a node at the drop point
                if (_edge.output == null || !(_edge.output.userData is string _outputId)) return;
                view.OpenSearch(view.PanelToScreen(_position), _outputId);
            }

            public void OnDrop(GraphView _graphView, Edge _edge) {
                view.ApplyDroppedEdge(_edge);
            }
        }

        private void ApplyDroppedEdge(Edge _edge) {
            if (!CanEdit) return;
            ApplyEdge(_edge);
            Rebuild();
        }
        #endregion
    }
}
