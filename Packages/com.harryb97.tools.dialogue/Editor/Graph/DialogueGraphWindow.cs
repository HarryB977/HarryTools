using System.Collections.Generic;
using System.Linq;
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
        private const string NoRunText = "No dialogue running";

        // Survives domain reloads; the asset reference itself is looked up again from it
        [SerializeField] private string assetGuid = "";
        [SerializeField] private bool snapToGrid;
        [SerializeField] private bool showMiniMap = true;

        [System.NonSerialized] private DialogueAsset asset;
        private ObjectField assetField;

        // Play mode
        [System.NonSerialized] private DialogueRunTracker tracker;
        [System.NonSerialized] private bool follow;
        // Set while the window moves the camera itself, so only other moves turn follow off
        [System.NonSerialized] private bool framing;
        // The node last framed by follow, so the camera only moves when the run does
        [System.NonSerialized] private string framedNodeId;
        [System.NonSerialized] private DialogueAsset framedAsset;
        private ToolbarMenu runMenu;
        private ToolbarToggle followToggle;
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

        /// <summary> The list of validator findings at the bottom </summary>
        internal DialogueIssueList IssueList { get; private set; }

        /// <summary> The issue count in the bar </summary>
        internal Label IssueCountLabel { get; private set; }

        /// <summary> The runs followed in Play. Null outside Play </summary>
        internal DialogueRunTracker Tracker => tracker;

        /// <summary> The camera centers on the node of the followed run each time it changes, and the window shows the dialogue that run is in </summary>
        internal bool Follow {
            get => follow;
            set {
                if (value == follow) return;
                follow = value;
                followToggle?.SetValueWithoutNotify(value);
                if (!follow) return;
                framedNodeId = null;
                OnRunChanged();
            }
        }
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
            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;
            ObjectChangeEvents.changesPublished -= OnObjectChanges;
            ObjectChangeEvents.changesPublished += OnObjectChanges;
            DialogueTables.SourceTextWritten -= OnSourceTextWritten;
            DialogueTables.SourceTextWritten += OnSourceTextWritten;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            if (asset == null && !string.IsNullOrEmpty(assetGuid)) {
                asset = AssetDatabase.LoadAssetAtPath<DialogueAsset>(AssetDatabase.GUIDToAssetPath(assetGuid));
            }
            // Opened in Play, or reloaded while entering it: read only before the dialogue loads, so loading cannot lay it out
            bool _playing = EditorApplication.isPlaying;
            if (_playing) {
                View.ReadOnly = true;
                Panel.ReadOnly = true;
            }
            ShowAsset();
            if (_playing) BeginPlayMode();
        }

        internal void OnDisable() {
            Undo.undoRedoPerformed -= OnUndoRedo;
            ObjectChangeEvents.changesPublished -= OnObjectChanges;
            DialogueTables.SourceTextWritten -= OnSourceTextWritten;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            DisposeTracker();
            // Text typed on a node is kept when the window closes or reloads
            View?.CommitPendingText();
            rootVisualElement.Clear();
            View = null;
            Panel = null;
            Toolbar = null;
            Finder = null;
            IssueList = null;
            IssueCountLabel = null;
            assetField = null;
            runMenu = null;
            followToggle = null;
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
            // Opening a dialogue validates it without the delay
            IssueList?.ValidateNow();
        }

        // Only edits of this dialogue or of its text tables matter; the list waits for them to stop
        private void OnObjectChanges(ref ObjectChangeEventStream _stream) {
            if (asset == null || IssueList == null) return;
            for (int _i = 0; _i < _stream.length; _i++) {
                if (_stream.GetEventType(_i) != ObjectChangeKind.ChangeAssetObjectProperties) continue;
                _stream.GetChangeAssetObjectPropertiesEvent(_i, out ChangeAssetObjectPropertiesEventArgs _args);
#pragma warning disable CS0618 // InstanceIDToObject is obsolete in 6000.3 but required for 6000.0 compatibility.
                UnityEngine.Object _changed = EditorUtility.InstanceIDToObject(_args.instanceId);
#pragma warning restore CS0618
                if (NotifyObjectChanged(_changed)) return;
            }
        }

        /// <summary> Schedules a validation when the object is the shown dialogue or a string table or its shared data. Returns whether it did </summary>
        internal bool NotifyObjectChanged(UnityEngine.Object _changed) {
            if (asset == null || IssueList == null || _changed == null) return false;
            bool _relevant = _changed == asset || _changed is UnityEngine.Localization.Tables.StringTable || _changed is UnityEngine.Localization.Tables.SharedTableData;
            if (_relevant) IssueList.ScheduleValidation();
            return _relevant;
        }

        // Text typed on the canvas is written to the tables, which the filtered object changes may not report
        private void OnSourceTextWritten(DialogueAsset _asset) {
            if (_asset != null && _asset == asset) IssueList?.ScheduleValidation();
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

            runMenu = new ToolbarMenu { text = NoRunText, tooltip = "The running dialogue shown on the canvas" };
            runMenu.style.display = DisplayStyle.None;
            Toolbar.Add(runMenu);
            followToggle = new ToolbarToggle { text = "Follow", tooltip = "Center the view on the node the dialogue is on, and show the dialogue it is in. Moving the view turns it off", value = follow };
            followToggle.style.display = DisplayStyle.None;
            followToggle.RegisterValueChangedCallback(_event => Follow = _event.newValue);
            Toolbar.Add(followToggle);

            Toolbar.Add(new ToolbarSpacer { flex = true });
            IssueCountLabel = new Label { tooltip = "Warnings of the shown dialogue" };
            IssueCountLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            IssueCountLabel.style.marginRight = 6f;
            Toolbar.Add(IssueCountLabel);
            Finder = new DialogueNodeFinderField { AssetSource = () => asset };
            Finder.Picked += _nodeId => View?.SelectAndFrame(_nodeId);
            Toolbar.Add(Finder);

            View = new DialogueGraphView(this) { SnapToGrid = snapToGrid, MiniMapVisible = showMiniMap };
            View.style.flexGrow = 1f;
            Panel = new DialogueInspectorPanel();

            View.SelectionChanged += () => Panel?.Show(asset, View?.SelectedNodeId);
            Panel.NodeEdited += _nodeId => {
                View?.RefreshNode(_nodeId);
                IssueList?.ScheduleValidation();
            };
            Panel.OutputsEdited += _ => View?.Rebuild();

            IssueList = new DialogueIssueList { AssetSource = () => asset };
            IssueList.NodeChosen += _nodeId => View?.SelectAndFrame(_nodeId);
            IssueList.DialogueChosen += SetAsset;
            IssueList.IssuesChanged += OnIssuesChanged;
            // A rebuilt canvas has new node views: they get their marks back at once and are checked again after the delay
            View.Rebuilt += () => {
                ApplyWarningMarks();
                ApplyRunMarks();
                IssueList?.ScheduleValidation();
            };

            // The panel keeps its width when the window is resized
            TwoPaneSplitView _split = new TwoPaneSplitView(1, PanelWidth, TwoPaneSplitViewOrientation.Horizontal);
            _split.style.flexGrow = 1f;
            _split.Add(View);
            _split.Add(Panel);

            rootVisualElement.Add(Toolbar);
            rootVisualElement.Add(_split);
            rootVisualElement.Add(IssueList);
            // Last, so the results are drawn above the canvas
            rootVisualElement.Add(Finder.Popup);

            View.CameraMoved += OnCameraMoved;
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

        private void OnIssuesChanged() {
            if (IssueCountLabel != null) IssueCountLabel.text = asset != null ? DialogueIssueList.Summary(IssueList.Issues) : "";
            ApplyWarningMarks();
        }

        /// <summary> Puts the mark on the nodes that have warnings, with the messages as tooltip </summary>
        private void ApplyWarningMarks() {
            if (View == null || IssueList == null) return;
            Dictionary<string, string> _messages = new Dictionary<string, string>();
            IReadOnlyList<DialogueIssue> _issues = IssueList.Issues;
            for (int _i = 0; _i < _issues.Count; _i++) {
                DialogueIssue _issue = _issues[_i];
                if (_issue.Severity != DialogueIssueSeverity.Warning || string.IsNullOrEmpty(_issue.NodeId)) continue;
                _messages[_issue.NodeId] = _messages.TryGetValue(_issue.NodeId, out string _text) ? _text + "\n" + _issue.Message : _issue.Message;
            }
            foreach (DialogueNodeView _view in View.nodes.OfType<DialogueNodeView>()) {
                _view.SetWarnings(_messages.TryGetValue(_view.NodeId, out string _text) ? _text : null);
            }
        }

        private void OnKeyDown(KeyDownEvent _event) {
            if (!_event.actionKey || _event.shiftKey || _event.altKey || _event.keyCode != KeyCode.F || DialogueGraphView.IsTextInput(_event.target)) return;
            Finder?.FocusField();
            _event.StopPropagation();
        }
        #endregion

        // ==================== PLAY MODE ====================
        #region Play Mode
        /// <summary> Starts following the running dialogues: the canvas and the panel turn read only, and follow is turned on </summary>
        internal void BeginPlayMode() {
            DisposeTracker();
            tracker = new DialogueRunTracker();
            tracker.Changed += OnRunChanged;
            follow = true;
            followToggle?.SetValueWithoutNotify(true);
            framedNodeId = null;
            framedAsset = null;
            SetPlayControlsVisible(true);
            if (Panel != null) Panel.ReadOnly = true;
            if (View != null) View.ReadOnly = true;
            OnRunChanged();
        }

        /// <summary> Stops following: the marks go and editing comes back </summary>
        internal void EndPlayMode() {
            DisposeTracker();
            SetPlayControlsVisible(false);
            if (Panel != null) Panel.ReadOnly = false;
            if (View != null) View.ReadOnly = false;
            ApplyRunMarks();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange _change) {
            // Entering Play clears the runtime's handlers after a reload, so the tracker is made again once Play has started
            if (_change == PlayModeStateChange.EnteredPlayMode) BeginPlayMode();
            else if (_change == PlayModeStateChange.ExitingPlayMode) EndPlayMode();
        }

        private void DisposeTracker() {
            tracker?.Dispose();
            tracker = null;
        }

        private void SetPlayControlsVisible(bool _visible) {
            DisplayStyle _display = _visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (runMenu != null) runMenu.style.display = _display;
            if (followToggle != null) followToggle.style.display = _display;
        }

        private void OnRunChanged() {
            if (tracker == null) return;
            RefreshRunMenu();

            // An empty window shows the followed dialogue; one showing another dialogue only with follow
            DialogueAsset _runAsset = tracker.CurrentAsset;
            if (_runAsset != null && _runAsset != asset && (asset == null || follow)) SetAsset(_runAsset);
            ApplyRunMarks();
            if (follow) FrameCurrent();
        }

        /// <summary> Lights up the node the followed run is on and the connections it went through in the shown dialogue </summary>
        private void ApplyRunMarks() {
            if (View == null) return;
            string _current = tracker != null && asset != null && tracker.CurrentAsset == asset ? tracker.CurrentNodeId : null;
            IReadOnlyCollection<string> _traversed = tracker != null ? tracker.TraversedOutputIds(asset) : null;
            foreach (DialogueNodeView _view in View.nodes.OfType<DialogueNodeView>()) _view.SetCurrent(_current != null && _view.NodeId == _current);
            foreach (DialogueEdge _edge in View.edges.OfType<DialogueEdge>()) _edge.Traversed = _traversed != null && _edge.OutputId != null && _traversed.Contains(_edge.OutputId);
        }

        private void FrameCurrent() {
            string _nodeId = tracker?.CurrentNodeId;
            if (View == null || _nodeId == null || tracker.CurrentAsset != asset) return;
            if (_nodeId == framedNodeId && asset == framedAsset) return;
            framedNodeId = _nodeId;
            framedAsset = asset;
            framing = true;
            try {
                View.Frame(_nodeId);
            } finally {
                framing = false;
            }
        }

        private void OnCameraMoved() {
            if (framing || tracker == null) return;
            Follow = false;
        }

        /// <summary> The runs in the bar's menu: the main one first, then the ambient ones </summary>
        private void RefreshRunMenu() {
            if (runMenu == null || tracker == null) return;
            DialogueRun _followed = tracker.Followed;
            runMenu.text = _followed != null ? RunLabel(_followed) : NoRunText;

            List<DialogueRun> _runs = MenuRuns(_followed, Dialogue.Active);
            List<DropdownMenuItem> _items = runMenu.menu.MenuItems();
            _items.Clear();
            HashSet<string> _labels = new HashSet<string>();
            for (int _i = 0; _i < _runs.Count; _i++) {
                DialogueRun _run = _runs[_i];
                string _label = RunLabel(_run);
                // Equal labels would merge into one item
                for (int _n = 2; !_labels.Add(_label); _n++) _label = RunLabel(_run) + " (" + _n + ")";
                runMenu.menu.AppendAction(_label, _ => {
                    if (tracker != null) tracker.Followed = _run;
                }, _run == _followed ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            }
            if (_runs.Count == 0) runMenu.menu.AppendAction(NoRunText, null, DropdownMenuAction.Status.Disabled);
        }

        /// <summary> The runs listed in the menu, in order: main runs, then the followed run once it ended, then the ambient ones </summary>
        internal static List<DialogueRun> MenuRuns(DialogueRun _followed, IReadOnlyList<DialogueRun> _active) {
            List<DialogueRun> _runs = _active.OrderBy(_run => _run.IsMain ? 0 : 1).ToList();
            if (_followed != null && !_runs.Contains(_followed)) _runs.Insert(_runs.Count(_run => _run.IsMain), _followed);
            return _runs;
        }

        /// <summary> "Main · dialogue · actor" for the main run, "dialogue · actor" for the others </summary>
        internal static string RunLabel(DialogueRun _run) {
            string _dialogue = _run.Asset != null ? _run.Asset.name : "(missing)";
            string _label = (_run.IsMain ? "Main · " : "") + _dialogue + " · " + ActorName(_run);
            return _run.State == RunState.Ended ? _label + " (ended)" : _label;
        }

        /// <summary> The object that started the run, else its first bound actor </summary>
        private static string ActorName(DialogueRun _run) {
            GameObject _origin = _run.Options?.Origin;
            if (_origin != null) return _origin.name;
            List<ActorBinding> _actors = _run.Options?.Actors;
            if (_actors != null) {
                for (int _i = 0; _i < _actors.Count; _i++) {
                    if (_actors[_i] != null && _actors[_i].Actor != null) return _actors[_i].Actor.name;
                }
            }
            return "no actor";
        }
        #endregion
    }
}
