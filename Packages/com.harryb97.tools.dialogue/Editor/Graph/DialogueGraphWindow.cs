using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HarryTools.Dialogue.Editor {
    /// <summary> Editor window that shows one dialogue as a graph. Changes are written to the asset at once, with undo </summary>
    internal sealed class DialogueGraphWindow : EditorWindow {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string WindowTitle = "Dialogue Graph";
        private const float PanelWidth = 320f;
        private const string FindCommand = "Find";

        // Survives domain reloads; the asset reference itself is looked up again from it
        [SerializeField] private string assetGuid = "";
        [SerializeField] private bool snapToGrid;
        [SerializeField] private bool showMiniMap = true;

        [System.NonSerialized] private DialogueAsset asset;
        private ObjectField assetField;
        #endregion

        #region Public Variables
        internal DialogueAsset Asset => asset;

        internal DialogueGraphView View { get; private set; }

        /// <summary> The side panel with the properties of the selected node </summary>
        internal DialogueInspectorPanel Panel { get; private set; }

        /// <summary> The bar above the canvas. Other parts of the window add their controls to it </summary>
        internal Toolbar Toolbar { get; private set; }

        /// <summary> The search of nodes in the bar </summary>
        internal DialogueNodeFinderField Finder { get; private set; }
        #endregion

        // ==================== OPEN ====================
        #region Open
        [MenuItem("Window/HarryTools/Dialogue Graph")]
        private static void OpenFromMenu() {
            GetWindow<DialogueGraphWindow>(WindowTitle);
        }

        internal static DialogueGraphWindow Open(DialogueAsset _asset) {
            DialogueGraphWindow _window = GetWindow<DialogueGraphWindow>(WindowTitle);
            _window.SetAsset(_asset);
            return _window;
        }

        [OnOpenAsset(1)]
        private static bool OpenFromAsset(int _instanceId, int _line) {
#pragma warning disable CS0618 // InstanceIDToObject is obsolete in 6000.3 but required for 6000.0 compatibility.
            if (!(EditorUtility.InstanceIDToObject(_instanceId) is DialogueAsset _asset)) return false;
#pragma warning restore CS0618
            Open(_asset);
            return true;
        }
        #endregion

        // ==================== UNITY ====================
        #region Unity
        // Internal so tests can run the same steps as a domain reload
        internal void OnEnable() {
            BuildLayout();
            Undo.undoRedoPerformed += OnUndoRedo;

            if (asset == null && !string.IsNullOrEmpty(assetGuid)) {
                asset = AssetDatabase.LoadAssetAtPath<DialogueAsset>(AssetDatabase.GUIDToAssetPath(assetGuid));
            }
            ShowAsset();
        }

        internal void OnDisable() {
            Undo.undoRedoPerformed -= OnUndoRedo;
            // Text typed on a node is kept when the window closes or reloads
            View?.CommitPendingText();
            rootVisualElement.Clear();
            View = null;
            Panel = null;
            Toolbar = null;
            Finder = null;
            assetField = null;
            asset = null;
        }

        private void OnLostFocus() {
            View?.CommitPendingText();
        }

        private void OnProjectChange() {
            if (AssetWasDeleted) SetAsset(null);
        }
        #endregion

        // ==================== ASSET ====================
        #region Asset
        /// <summary> Shows the dialogue in the window. Null leaves it empty </summary>
        internal void SetAsset(DialogueAsset _asset) {
            if (!AssetWasDeleted) View?.CommitPendingText();
            asset = _asset;
            assetGuid = _asset != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(_asset, out string _guid, out long _) ? _guid : "";
            ShowAsset();
        }

        /// <summary> The window still holds the asset object but Unity destroyed it </summary>
        private bool AssetWasDeleted => !ReferenceEquals(asset, null) && asset == null;

        private void ShowAsset() {
            titleContent = new GUIContent(asset != null ? asset.name : WindowTitle);
            assetField?.SetValueWithoutNotify(asset);
            Finder?.ClearQuery();
            View?.Load(asset);
            Panel?.Show(asset, View?.SelectedNodeId);
        }

        private void OnUndoRedo() {
            if (View == null) return;
            if (AssetWasDeleted) SetAsset(null);
            else View.Rebuild();
        }
        #endregion

        // ==================== LAYOUT ====================
        #region Layout
        private void BuildLayout() {
            rootVisualElement.Clear();

            Toolbar = new Toolbar();
            assetField = new ObjectField { objectType = typeof(DialogueAsset), allowSceneObjects = false, tooltip = "The dialogue shown in the window" };
            assetField.style.minWidth = 220f;
            assetField.RegisterValueChangedCallback(_event => SetAsset(_event.newValue as DialogueAsset));
            Toolbar.Add(assetField);

            ToolbarToggle _snap = new ToolbarToggle { text = "Snap to Grid", tooltip = "Moved nodes land on the grid", value = snapToGrid };
            _snap.RegisterValueChangedCallback(_event => {
                snapToGrid = _event.newValue;
                if (View != null) View.SnapToGrid = snapToGrid;
            });
            Toolbar.Add(_snap);

            ToolbarToggle _miniMap = new ToolbarToggle { text = "Mini Map", tooltip = "Show an overview of the whole dialogue", value = showMiniMap };
            _miniMap.RegisterValueChangedCallback(_event => {
                showMiniMap = _event.newValue;
                if (View != null) View.MiniMapVisible = showMiniMap;
            });
            Toolbar.Add(_miniMap);

            Toolbar.Add(new ToolbarSpacer { flex = true });
            Finder = new DialogueNodeFinderField { AssetSource = () => asset };
            Finder.Picked += _nodeId => View?.SelectAndFrame(_nodeId);
            Toolbar.Add(Finder);

            View = new DialogueGraphView(this) { SnapToGrid = snapToGrid, MiniMapVisible = showMiniMap };
            View.style.flexGrow = 1f;
            Panel = new DialogueInspectorPanel();

            View.SelectionChanged += () => Panel?.Show(asset, View?.SelectedNodeId);
            Panel.NodeEdited += _nodeId => View?.RefreshNode(_nodeId);
            Panel.OutputsEdited += _ => View?.Rebuild();

            // The panel keeps its width when the window is resized
            TwoPaneSplitView _split = new TwoPaneSplitView(1, PanelWidth, TwoPaneSplitViewOrientation.Horizontal);
            _split.style.flexGrow = 1f;
            _split.Add(View);
            _split.Add(Panel);

            rootVisualElement.Add(Toolbar);
            rootVisualElement.Add(_split);
            // Last, so the results are drawn above the canvas
            rootVisualElement.Add(Finder.Popup);

            View.RegisterCallback<PointerDownEvent>(_ => Finder?.HideResults(), TrickleDown.TrickleDown);
            rootVisualElement.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            rootVisualElement.RegisterCallback<ValidateCommandEvent>(_event => {
                if (_event.commandName == FindCommand && !DialogueGraphView.IsTextInput(_event.target)) _event.StopPropagation();
            });
            rootVisualElement.RegisterCallback<ExecuteCommandEvent>(_event => {
                if (_event.commandName != FindCommand || DialogueGraphView.IsTextInput(_event.target)) return;
                Finder?.FocusField();
                _event.StopPropagation();
            });
        }

        private void OnKeyDown(KeyDownEvent _event) {
            if (!_event.actionKey || _event.shiftKey || _event.altKey || _event.keyCode != KeyCode.F || DialogueGraphView.IsTextInput(_event.target)) return;
            Finder?.FocusField();
            _event.StopPropagation();
        }
        #endregion
    }
}
