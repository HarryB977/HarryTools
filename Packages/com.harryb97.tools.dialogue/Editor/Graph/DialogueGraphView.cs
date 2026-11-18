using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Canvas of a dialogue. The asset is the only source of truth: every edit goes through <see cref="DialogueGraphOps"/> and the canvas is rebuilt from the asset </summary>
    internal sealed class DialogueGraphView : GraphView {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string StyleSheetPath = "Packages/com.harryb97.tools.dialogue/Editor/Graph/DialogueGraph.uss";
        private const string CameraKeyPrefix = "HarryTools.Dialogue.Graph.Camera.";
        private const float GridSize = 20f;
        private const long FrameRetryMilliseconds = 50;

        private readonly Dictionary<string, DialogueNodeView> nodeViews = new Dictionary<string, DialogueNodeView>();
        private readonly List<NodeOutput> outputBuffer = new List<NodeOutput>();
        private readonly EditorWindow window;
        private readonly EdgeListener edgeListener;
        private readonly MiniMap miniMap;
        private DialogueNodeSearch search;
        private bool restoringCamera;
        #endregion

        #region Public Variables
        internal DialogueAsset Asset { get; private set; }

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
            viewTransformChanged = _ => SaveCamera();

            RegisterCallback<DetachFromPanelEvent>(_ => DestroySearch());
        }
        #endregion

        // ==================== ASSET ====================
        #region Asset
        /// <summary> Shows the asset, with the camera it had last time in this editor session. Null leaves the canvas empty </summary>
        internal void Load(DialogueAsset _asset) {
            Asset = _asset;
            Rebuild();
            RestoreCamera();
        }

        /// <summary> Recreates every node and edge from the asset, keeping the camera and the selected nodes </summary>
        internal void Rebuild() {
            HashSet<string> _selected = new HashSet<string>();
            foreach (ISelectable _item in selection) {
                if (_item is DialogueNodeView _view) _selected.Add(_view.NodeId);
            }

            ClearSelection();
            foreach (GraphElement _element in graphElements.ToList()) RemoveElement(_element);
            nodeViews.Clear();

            if (Asset == null) return;

            for (int _i = 0; _i < Asset.Nodes.Count; _i++) {
                DialogueNode _node = Asset.Nodes[_i];
                // A node whose class no longer exists deserializes as null; the validator reports it
                if (_node == null || string.IsNullOrEmpty(_node.Id) || nodeViews.ContainsKey(_node.Id)) continue;
                DialogueNodeView _view = DialogueNodeView.Create(_node, edgeListener);
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
                    AddElement(_port.ConnectTo(_target.Input));
                }
            }

            foreach (string _id in _selected) {
                if (nodeViews.TryGetValue(_id, out DialogueNodeView _view)) AddToSelection(_view);
            }
        }

        internal DialogueNodeView FindNodeView(string _id) {
            if (string.IsNullOrEmpty(_id)) return null;
            return nodeViews.TryGetValue(_id, out DialogueNodeView _view) ? _view : null;
        }

        /// <summary> Centers the camera on the node, keeping the zoom </summary>
        internal void Frame(string _nodeId) {
            DialogueNodeView _view = FindNodeView(_nodeId);
            if (_view == null) return;

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
            if (Asset == null) return null;
            if (SnapToGrid) _position = Snap(_position);

            DialogueNode _node = string.IsNullOrEmpty(_fromOutputId)
                ? DialogueGraphOps.CreateNode(Asset, _type, _position)
                : DialogueGraphOps.CreateConnected(Asset, _type, _position, _fromOutputId);
            Rebuild();
            return _node;
        }

        internal void Connect(string _outputId, string _targetNodeId) {
            if (Asset == null) return;
            DialogueGraphOps.Connect(Asset, _outputId, _targetNodeId);
            Rebuild();
        }

        /// <summary> Opens the node type search at the screen position. The created node is connected to the output when one is given </summary>
        internal void OpenSearch(Vector2 _screenPosition, string _fromOutputId) {
            if (Asset == null) return;
            if (search == null) {
                search = ScriptableObject.CreateInstance<DialogueNodeSearch>();
                search.hideFlags = HideFlags.HideAndDontSave;
            }
            search.Setup(this, _fromOutputId);
            SearchWindow.Open(new SearchWindowContext(_screenPosition), search);
        }

        /// <summary> The canvas position under a screen position </summary>
        internal Vector2 ScreenToCanvas(Vector2 _screenPosition) {
            return contentViewContainer.WorldToLocal(_screenPosition - window.position.position);
        }

        /// <summary> The screen position of a point of the window's panel </summary>
        private Vector2 PanelToScreen(Vector2 _panelPosition) {
            return _panelPosition + window.position.position;
        }
        #endregion

        // ==================== GRAPH VIEW ====================
        #region Graph View
        public override List<Port> GetCompatiblePorts(Port _startPort, NodeAdapter _nodeAdapter) {
            List<Port> _compatible = new List<Port>();
            foreach (Port _port in ports) {
                if (_port.direction != _startPort.direction && _port.node != _startPort.node) _compatible.Add(_port);
            }
            return _compatible;
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent _event) {
            // A right click on the empty canvas goes straight to the node search
            if (_event.target == this) {
                OpenSearch(PanelToScreen(_event.mousePosition), null);
                return;
            }
            base.BuildContextualMenu(_event);
        }

        private GraphViewChange OnGraphViewChanged(GraphViewChange _change) {
            if (Asset == null) return _change;

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

        private void ApplyMoves(List<GraphElement> _moved) {
            Dictionary<string, Vector2> _positions = new Dictionary<string, Vector2>();
            for (int _i = 0; _i < _moved.Count; _i++) {
                if (!(_moved[_i] is DialogueNodeView _view)) continue;
                Vector2 _position = _view.CanvasPosition;
                if (SnapToGrid) {
                    _position = Snap(_position);
                    _view.SetPosition(new Rect(_position, Vector2.zero));
                }
                _positions[_view.NodeId] = _position;
            }
            DialogueGraphOps.Move(Asset, _positions);
        }

        private void ApplyRemovals(List<GraphElement> _removed) {
            List<string> _ids = new List<string>();
            List<string> _disconnected = new List<string>();
            for (int _i = 0; _i < _removed.Count; _i++) {
                if (_removed[_i] is DialogueNodeView _view) _ids.Add(_view.NodeId);
                else if (_removed[_i] is Edge _edge && _edge.output != null && _edge.output.userData is string _outputId) _disconnected.Add(_outputId);
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

            // Framing needs the size of the nodes, known after the next layout pass
            DialogueAsset _framed = Asset;
            bool _done = false;
            schedule.Execute(() => {
                if (Asset == null || Asset != _framed) {
                    _done = true;
                    return;
                }
                foreach (DialogueNodeView _view in nodeViews.Values) {
                    if (float.IsNaN(_view.layout.width) || _view.layout.width <= 0f) return;
                }
                FrameAll();
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
            if (Asset == null) return;
            ApplyEdge(_edge);
            Rebuild();
        }
        #endregion
    }
}
