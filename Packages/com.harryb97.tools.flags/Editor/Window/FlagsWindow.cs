using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HarryTools.Flags.Editor
{
    /// <summary>
    /// Lists every flag of the project grouped by scope and scene, with uses, warnings and, during Play, live values and history.
    /// Outside Play the checkbox edits the initial value; in Play it changes the current value as a live change.
    /// </summary>
    internal sealed class FlagsWindow : EditorWindow
    {
        // ==================== VARIABLES ====================

        private const string RenameControl = "FlagsWindowRename";
        private const string CreateControl = "FlagsWindowCreate";
        private const string DetailNameControl = "FlagsWindowDetailName";
        private const string DetailDescriptionControl = "FlagsWindowDetailDescription";
        private const float FlashSeconds = 1f;
        private const float RowHeight = 20f;
        private const float ToggleWidth = 18f;
        private const float UsesWidth = 60f;
        private const float InfoWidth = 150f;
        private const int DeleteDialogMaxUses = 15;
        private const string PlayLockedTooltip = "Edit outside Play mode.";

        private static readonly Color SelectedColor = new Color(0.17f, 0.36f, 0.53f, 1f);
        private static readonly Color FlashColor = new Color(0.55f, 0.5f, 0.15f, 1f);
        private static readonly Color HeaderColor = new Color(0f, 0f, 0f, 0.15f);
        private static readonly Color WarningColor = new Color(0.88f, 0.69f, 0.31f);
        private static readonly Color ErrorColor = new Color(0.95f, 0.35f, 0.35f);
        private static readonly string[] FilterLabels = { "All", "Global", "Local" };

        [SerializeField] private string search = string.Empty;
        [SerializeField] private FlagFilter filter = FlagFilter.All;
        [SerializeField] private string selectedGuid = string.Empty;
        [SerializeField] private string localSceneGuid = string.Empty;
        [SerializeField] private Vector2 listScroll;
        [SerializeField] private Vector2 detailScroll;
        [SerializeField] private double playStartTime;

        private SearchField searchField;
        private List<FlagGroup> groups = new List<FlagGroup>();
        private readonly Dictionary<string, RowInfo> rows = new Dictionary<string, RowInfo>(StringComparer.Ordinal);
        private bool dirty = true;
        private int lastFlagCount = -1;
        private double lastPlayRepaint;

        private List<SceneOption> projectScenes = new List<SceneOption>();

        private string renamingGuid;
        private string renameText;
        private string renameError;
        private bool focusRename;

        private bool creating;
        private FlagScope createScope;
        private string createSceneGuid;
        private bool createSceneLocked;
        private string createName = string.Empty;
        private string createError;
        private bool focusCreate;
        private Action<FlagDefinition> onCreated;

        private string detailError;
        [NonSerialized] private readonly FlagDetailEdit detailEdit = new FlagDetailEdit();
        [NonSerialized] private bool detailWasFocused;

        // Not kept across domain reloads: the window would come back with empty styles.
        [NonSerialized] private GUIStyle mutedStyle;
        [NonSerialized] private GUIStyle warningStyle;
        [NonSerialized] private GUIStyle errorStyle;
        [NonSerialized] private GUIStyle rowErrorStyle;
        [NonSerialized] private GUIStyle linkStyle;
        [NonSerialized] private GUIStyle historyStyle;

        private sealed class RowInfo
        {
            public int Uses;
            public IReadOnlyList<string> Warnings;
        }

        private struct SceneOption
        {
            public string Guid;
            public string Name;
        }

        // ==================== OPENING ====================

        [MenuItem("Window/HarryTools/Flags")]
        public static void Open() => GetOrOpen();

        /// <summary>
        /// Opens the window with the creation of a flag ready for the given scope and scene. <paramref name="_onCreated"/> receives the
        /// flag once it is created; with a callback the scene cannot be changed, so the flag stays usable by the caller.
        /// </summary>
        internal static void OpenForCreate(FlagScope _scope, string _sceneGuid, Action<FlagDefinition> _onCreated)
        {
            FlagsWindow _window = GetOrOpen();
            _window.BeginCreate(_scope, _sceneGuid, _onCreated != null && !string.IsNullOrEmpty(_sceneGuid), _onCreated);
        }

        private static FlagsWindow GetOrOpen()
        {
            FlagsWindow _window = GetWindow<FlagsWindow>();
            _window.titleContent = new GUIContent("Flags");
            _window.minSize = new Vector2(640f, 300f);
            _window.Show();
            _window.Focus();
            return _window;
        }

        // ==================== UNITY ====================

        private void OnEnable()
        {
            searchField = new SearchField();
            Subscribe();
            RefreshProjectScenes();
            MarkDirty();
        }

        private void OnDisable() => Unsubscribe();

        private void OnFocus() => MarkDirty();

        private void OnLostFocus()
        {
            if (detailWasFocused) CommitDetailEdit();
            detailWasFocused = false;
        }

        private void Update()
        {
            if (!EditorApplication.isPlaying) return;

            // "changed Ns ago" and the row flash depend on time only, so Play needs a steady repaint.
            double _now = Time.realtimeSinceStartupAsDouble;
            if (_now - lastPlayRepaint < 0.25) return;
            lastPlayRepaint = _now;
            Repaint();
        }

        private void OnGUI()
        {
            EnsureStyles();
            if (EditorApplication.isPlaying) playStartTime = Time.realtimeSinceStartupAsDouble - Time.unscaledTimeAsDouble;

            FlagDatabase _database = FlagDatabaseLocator.Find();
            FlagUsageIndex _index = FlagUsageIndexLocator.Find();
            if (_database != null && _database.Flags.Count != lastFlagCount) dirty = true;
            if (dirty) Rebuild(_database, _index);

            HandleKeyboard(_database);
            DrawToolbar(_database);
            if (creating) DrawCreateBar();

            if (_database == null)
            {
                EditorGUILayout.HelpBox("This project has no flags yet. Create one with + Global or + Local.", MessageType.Info);
                return;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(position.width * 0.58f)))
                    DrawList(_database, _index);

                Rect _line = GUILayoutUtility.GetRect(1f, 1f, GUILayout.ExpandHeight(true), GUILayout.Width(1f));
                EditorGUI.DrawRect(_line, new Color(0f, 0f, 0f, 0.35f));

                using (new EditorGUILayout.VerticalScope())
                    DrawDetail(_database, _index);
            }
        }

        // ==================== EVENTS ====================

        // Flags.Changed is not used: its handlers are cleared on entering Play. FlagHistory re-subscribes itself and raises Updated.
        private void Subscribe()
        {
            Unsubscribe();
            FlagHistory.Updated += Repaint;
            FlagUsageMaintainer.Changed += MarkDirty;
            Undo.undoRedoPerformed += MarkDirty;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.projectChanged += OnProjectChanged;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorSceneManager.sceneClosed += OnSceneClosed;
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
            SubscribeRuntimeSceneEvents();
        }

        // Scenes the game loads in Play only raise the runtime events. Subscribed again on entering Play, like the other runtime hooks.
        private void SubscribeRuntimeSceneEvents()
        {
            SceneManager.sceneLoaded -= OnRuntimeSceneLoaded;
            SceneManager.sceneUnloaded -= OnRuntimeSceneUnloaded;
            SceneManager.sceneLoaded += OnRuntimeSceneLoaded;
            SceneManager.sceneUnloaded += OnRuntimeSceneUnloaded;
        }

        private void Unsubscribe()
        {
            FlagHistory.Updated -= Repaint;
            FlagUsageMaintainer.Changed -= MarkDirty;
            Undo.undoRedoPerformed -= MarkDirty;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.projectChanged -= OnProjectChanged;
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorSceneManager.sceneClosed -= OnSceneClosed;
            EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
            SceneManager.sceneLoaded -= OnRuntimeSceneLoaded;
            SceneManager.sceneUnloaded -= OnRuntimeSceneUnloaded;
        }

        private void MarkDirty()
        {
            dirty = true;
            Repaint();
        }

        private void OnPlayModeStateChanged(PlayModeStateChange _state)
        {
            if (_state == PlayModeStateChange.EnteredPlayMode) SubscribeRuntimeSceneEvents();
            MarkDirty();
        }

        private void OnRuntimeSceneLoaded(Scene _scene, LoadSceneMode _mode) => MarkDirty();

        private void OnRuntimeSceneUnloaded(Scene _scene) => MarkDirty();

        private void OnProjectChanged()
        {
            RefreshProjectScenes();
            MarkDirty();
        }

        private void OnSceneOpened(Scene _scene, OpenSceneMode _mode) => MarkDirty();

        private void OnSceneClosed(Scene _scene) => MarkDirty();

        private void OnActiveSceneChanged(Scene _previous, Scene _current)
        {
            localSceneGuid = string.Empty;
            Repaint();
        }

        // ==================== TOOLBAR ====================

        private void DrawToolbar(FlagDatabase _database)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                string _search = searchField.OnToolbarGUI(search, GUILayout.MinWidth(80f));
                if (_search != search)
                {
                    search = _search;
                    MarkDirty();
                }

                FlagFilter _filter = (FlagFilter)GUILayout.Toolbar((int)filter, FilterLabels, EditorStyles.toolbarButton, GUILayout.Width(150f));
                if (_filter != filter)
                {
                    filter = _filter;
                    MarkDirty();
                }

                GUILayout.Space(6f);
                if (GUILayout.Button("+ Global", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                    BeginCreate(FlagScope.Global, string.Empty, false, null);

                if (GUILayout.Button("+ Local", EditorStyles.toolbarButton, GUILayout.Width(55f)))
                    BeginCreate(FlagScope.Local, LocalSceneGuid(), false, null);
                DrawScenePopup(LocalSceneGuid(), _guid => localSceneGuid = _guid, EditorStyles.toolbarPopup, 120f);

                GUILayout.FlexibleSpace();

                if (EditorApplication.isPlaying)
                {
                    Color _previous = GUI.contentColor;
                    GUI.contentColor = new Color(0.5f, 0.82f, 0.5f);
                    GUILayout.Label("● Play: live", EditorStyles.toolbarButton);
                    GUI.contentColor = _previous;
                }

                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || _database == null))
                {
                    if (GUILayout.Button(new GUIContent("Rebuild Usage Index", "Reads every scene, prefab and ScriptableObject again."), EditorStyles.toolbarButton))
                    {
                        FlagUsageMaintainer.Rebuild();
                        MarkDirty();
                    }
                }
            }
        }

        // ==================== CREATION ====================

        private void BeginCreate(FlagScope _scope, string _sceneGuid, bool _sceneLocked, Action<FlagDefinition> _onCreated)
        {
            creating = true;
            createScope = _scope;
            createSceneGuid = _sceneGuid ?? string.Empty;
            createSceneLocked = _sceneLocked;
            createName = string.Empty;
            createError = null;
            focusCreate = true;
            onCreated = _onCreated;
            CancelRename();
            Repaint();
        }

        private void EndCreate()
        {
            creating = false;
            onCreated = null;
            createError = null;
            GUI.FocusControl(null);
        }

        private void DrawCreateBar()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    // A caller that only accepts globals and the locals of one scene may still choose between those two.
                    bool _canSwitch = onCreated == null || createSceneLocked;
                    using (new EditorGUI.DisabledScope(!_canSwitch))
                    {
                        createScope = (FlagScope)EditorGUILayout.EnumPopup(createScope, GUILayout.Width(70f));
                    }

                    if (createScope == FlagScope.Local)
                    {
                        using (new EditorGUI.DisabledScope(createSceneLocked))
                        {
                            if (string.IsNullOrEmpty(createSceneGuid)) createSceneGuid = LocalSceneGuid();
                            DrawScenePopup(createSceneGuid, _guid => createSceneGuid = _guid, EditorStyles.popup, 140f);
                        }
                    }

                    bool _submit = Event.current.type == EventType.KeyDown && GUI.GetNameOfFocusedControl() == CreateControl
                        && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
                    bool _cancel = Event.current.type == EventType.KeyDown && GUI.GetNameOfFocusedControl() == CreateControl && Event.current.keyCode == KeyCode.Escape;
                    if (_submit || _cancel) Event.current.Use();

                    GUI.SetNextControlName(CreateControl);
                    createName = EditorGUILayout.TextField(createName);
                    if (focusCreate && Event.current.type == EventType.Repaint)
                    {
                        EditorGUI.FocusTextInControl(CreateControl);
                        focusCreate = false;
                    }

                    if (GUILayout.Button("Create", GUILayout.Width(60f)) || _submit) CommitCreate();
                    if (GUILayout.Button("Cancel", GUILayout.Width(60f)) || _cancel) EndCreate();
                }

                if (!string.IsNullOrEmpty(createError)) GUILayout.Label(createError, errorStyle);
            }
        }

        private void CommitCreate()
        {
            if (!creating) return;
            if (createScope == FlagScope.Local && string.IsNullOrEmpty(createSceneGuid))
            {
                createError = "Pick a saved scene for the local flag.";
                return;
            }

            // Checked before GetOrCreate so a bad name never creates the database on its own.
            if (!FlagNaming.IsValidName(createName))
            {
                createError = "Flag names can only contain letters, digits and underscores.";
                return;
            }

            FlagDatabase _database = FlagDatabaseLocator.GetOrCreate();
            if (!FlagDatabaseEditing.TryCreate(_database, createScope, createSceneGuid, createName, out FlagDefinition _created, out string _error))
            {
                createError = _error;
                return;
            }

            Action<FlagDefinition> _callback = onCreated;
            EndCreate();
            Select(_created.Guid);
            MarkDirty();
            _callback?.Invoke(_created);
        }

        // ==================== LIST ====================

        private void DrawList(FlagDatabase _database, FlagUsageIndex _index)
        {
            listScroll = EditorGUILayout.BeginScrollView(listScroll);

            if (groups.Count == 0)
            {
                string _message = _database.Flags.Count == 0 ? "No flags yet. Create one with + Global or + Local." : "No flags match the search.";
                EditorGUILayout.HelpBox(_message, MessageType.None);
            }

            foreach (FlagGroup _group in groups)
            {
                DrawGroupHeader(_group);
                foreach (FlagDefinition _flag in _group.Flags) DrawRow(_database, _flag, _group.SceneMissing);
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawGroupHeader(FlagGroup _group)
        {
            Rect _rect = GUILayoutUtility.GetRect(0f, RowHeight, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint) EditorGUI.DrawRect(_rect, HeaderColor);

            string _suffix = _group.SceneMissing ? "  (scene was deleted)"
                : string.IsNullOrEmpty(_group.SceneGuid) ? string.Empty
                : _group.SceneLoaded ? "  (scene)" : "  (scene, not loaded)";

            Rect _label = new Rect(_rect.x + 6f, _rect.y, _rect.width - 6f, _rect.height);
            GUIContent _title = new GUIContent(_group.Title);
            float _titleWidth = EditorStyles.boldLabel.CalcSize(_title).x;
            GUI.Label(_label, _title, EditorStyles.boldLabel);
            GUI.Label(new Rect(_label.x + _titleWidth, _label.y, _label.width - _titleWidth, _label.height), _suffix, _group.SceneMissing ? warningStyle : mutedStyle);
        }

        private void DrawRow(FlagDatabase _database, FlagDefinition _flag, bool _sceneMissing)
        {
            Rect _rect = GUILayoutUtility.GetRect(0f, RowHeight, GUILayout.ExpandWidth(true));
            rows.TryGetValue(_flag.Guid, out RowInfo _info);
            bool _playing = EditorApplication.isPlaying;
            double _lastChange = _playing ? FlagHistory.LastChangeTime(_flag.Guid) : -1;
            double _age = _lastChange >= 0 ? Time.realtimeSinceStartupAsDouble - _lastChange : double.MaxValue;
            Event _event = Event.current;

            if (_event.type == EventType.Repaint)
            {
                if (_age < FlashSeconds) EditorGUI.DrawRect(_rect, FlashColor);
                else if (_flag.Guid == selectedGuid) EditorGUI.DrawRect(_rect, SelectedColor);
            }

            Rect _toggle = new Rect(_rect.x + 16f, _rect.y + 1f, ToggleWidth, _rect.height - 2f);
            Rect _infoRect = new Rect(_rect.xMax - InfoWidth - 4f, _rect.y, InfoWidth, _rect.height);
            Rect _uses = new Rect(_infoRect.x - UsesWidth, _rect.y, UsesWidth, _rect.height);
            Rect _name = new Rect(_toggle.xMax + 4f, _rect.y + 1f, _uses.x - _toggle.xMax - 8f, _rect.height - 2f);

            DrawValueToggle(_database, _flag, _toggle, _playing);

            if (renamingGuid == _flag.Guid) DrawRenameField(_database, _name);
            else if (_sceneMissing) GUI.Label(_name, new GUIContent($"{_flag.Name}  (was in {_flag.SceneName})"));
            else GUI.Label(_name, _flag.Name);

            int _count = _info != null ? _info.Uses : 0;
            GUI.Label(_uses, _count == 1 ? "1 use" : _count + " uses", mutedStyle);

            string _warning = _info != null && _info.Warnings.Count > 0 ? _info.Warnings[0] : null;
            if (renamingGuid == _flag.Guid && !string.IsNullOrEmpty(renameError)) GUI.Label(_infoRect, new GUIContent(renameError, renameError), rowErrorStyle);
            else if (_lastChange >= 0) GUI.Label(_infoRect, new GUIContent("changed " + Ago(_age), _warning), mutedStyle);
            else if (_warning != null) GUI.Label(_infoRect, new GUIContent("⚠ " + _warning, string.Join("\n", _info.Warnings)), warningStyle);
            else GUI.Label(_infoRect, "—", mutedStyle);

            if (_event.type == EventType.MouseDown && _rect.Contains(_event.mousePosition) && !_toggle.Contains(_event.mousePosition))
            {
                if (renamingGuid == _flag.Guid && _name.Contains(_event.mousePosition)) return;

                if (_event.button == 0)
                {
                    Select(_flag.Guid);
                    if (_event.clickCount == 2) BeginRename(_flag);
                    _event.Use();
                }
                else if (_event.button == 1)
                {
                    Select(_flag.Guid);
                    ShowRowMenu(_database, _flag);
                    _event.Use();
                }
            }
        }

        private void DrawValueToggle(FlagDatabase _database, FlagDefinition _flag, Rect _rect, bool _playing)
        {
            bool _value = _playing ? Flags.GetValue(_flag.Guid) : _flag.InitialValue;
            string _tooltip = _playing ? "Current value. Changing it counts as a live change." : "Initial value.";

            EditorGUI.BeginChangeCheck();
            bool _next = EditorGUI.Toggle(_rect, _value);
            GUI.Label(_rect, new GUIContent(string.Empty, _tooltip));
            if (!EditorGUI.EndChangeCheck()) return;

            SetValue(_database, _flag, _next, _playing);
        }

        private void SetValue(FlagDatabase _database, FlagDefinition _flag, bool _value, bool _playing)
        {
            if (_playing) Flags.SetValue(new FlagRef(_flag.Guid, _flag.DisplayPath), _value, null);
            else FlagDatabaseEditing.SetInitialValue(_database, _flag.Guid, _value);
            Repaint();
        }

        private void ShowRowMenu(FlagDatabase _database, FlagDefinition _flag)
        {
            GenericMenu _menu = new GenericMenu();
            _menu.AddItem(new GUIContent("Rename"), false, () => BeginRename(_flag));
            if (EditorApplication.isPlaying) _menu.AddDisabledItem(new GUIContent("Delete (edit outside Play mode)"));
            else _menu.AddItem(new GUIContent("Delete"), false, () => Delete(_database, _flag));
            _menu.ShowAsContext();
        }

        // ==================== RENAME ====================

        private void BeginRename(FlagDefinition _flag)
        {
            renamingGuid = _flag.Guid;
            renameText = _flag.Name;
            renameError = null;
            focusRename = true;
            Repaint();
        }

        private void CancelRename()
        {
            if (renamingGuid == null) return;
            renamingGuid = null;
            renameError = null;
            GUI.FocusControl(null);
            Repaint();
        }

        private void DrawRenameField(FlagDatabase _database, Rect _rect)
        {
            Event _event = Event.current;
            if (_event.type == EventType.KeyDown && GUI.GetNameOfFocusedControl() == RenameControl)
            {
                if (_event.keyCode == KeyCode.Return || _event.keyCode == KeyCode.KeypadEnter)
                {
                    CommitRename(_database);
                    _event.Use();
                    return;
                }
                if (_event.keyCode == KeyCode.Escape)
                {
                    CancelRename();
                    _event.Use();
                    return;
                }
            }

            GUI.SetNextControlName(RenameControl);
            renameText = EditorGUI.TextField(_rect, renameText);
            if (focusRename && _event.type == EventType.Repaint)
            {
                EditorGUI.FocusTextInControl(RenameControl);
                focusRename = false;
            }
        }

        private void CommitRename(FlagDatabase _database)
        {
            if (renamingGuid == null) return;
            if (_database.TryGet(renamingGuid, out FlagDefinition _flag) && _flag.Name == renameText)
            {
                CancelRename();
                return;
            }

            if (!FlagDatabaseEditing.TryRename(_database, renamingGuid, renameText, out string _error))
            {
                renameError = _error;
                Repaint();
                return;
            }
            CancelRename();
            MarkDirty();
        }

        // ==================== DELETE ====================

        private void Delete(FlagDatabase _database, FlagDefinition _flag)
        {
            FlagUsageIndex _index = FlagUsageIndexLocator.Find();
            List<FlagUsageEntry> _uses = _index != null ? _index.For(_flag.Guid).ToList() : new List<FlagUsageEntry>();

            if (_uses.Count > 0 && !EditorUtility.DisplayDialog("Delete Flag", DeleteMessage(_flag, _uses), "Delete", "Cancel")) return;

            FlagDatabaseEditing.Delete(_database, _flag.Guid);
            if (selectedGuid == _flag.Guid) selectedGuid = string.Empty;
            if (renamingGuid == _flag.Guid) CancelRename();
            MarkDirty();
        }

        private static string DeleteMessage(FlagDefinition _flag, List<FlagUsageEntry> _uses)
        {
            StringBuilder _message = new StringBuilder();
            _message.AppendLine($"'{_flag.DisplayPath}' is used in {_uses.Count} place(s). Those references will break:");
            _message.AppendLine();
            foreach (FlagUsageEntry _use in _uses.Take(DeleteDialogMaxUses)) _message.AppendLine("• " + KindLabel(_use.kind) + ": " + UsageLabel(_use));
            if (_uses.Count > DeleteDialogMaxUses) _message.AppendLine($"• … and {_uses.Count - DeleteDialogMaxUses} more");
            _message.AppendLine();
            _message.Append("Delete it anyway? You can undo it.");
            return _message.ToString();
        }

        // ==================== DETAIL ====================

        private void DrawDetail(FlagDatabase _database, FlagUsageIndex _index)
        {
            if (string.IsNullOrEmpty(selectedGuid) || !_database.TryGet(selectedGuid, out FlagDefinition _flag))
            {
                GUILayout.Label("Select a flag to see its details.", mutedStyle);
                return;
            }

            SyncDetailEdit(_flag);

            detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
            bool _playing = EditorApplication.isPlaying;

            string _scopeText = _flag.Scope == FlagScope.Global ? "Global" : "Local of " + (string.IsNullOrEmpty(_flag.SceneName) ? "an unnamed scene" : _flag.SceneName);
            EditorGUILayout.LabelField(_flag.Name, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Scope", _scopeText);

            DrawDetailField(_flag, DetailNameControl, new GUIContent("Name"), detailEdit.Name, _value => detailEdit.Name = _value);
            if (!string.IsNullOrEmpty(detailError)) EditorGUILayout.HelpBox(detailError, MessageType.Error);

            // In Play an unwritten flag reads its initial value, so editing it would change the live value without a Changed event.
            using (new EditorGUI.DisabledScope(_playing))
            {
                EditorGUI.BeginChangeCheck();
                bool _initial = EditorGUILayout.Toggle(new GUIContent("Initial Value", _playing ? PlayLockedTooltip : string.Empty), _flag.InitialValue);
                if (EditorGUI.EndChangeCheck()) FlagDatabaseEditing.SetInitialValue(_database, _flag.Guid, _initial);
            }

            DrawDetailField(_flag, DetailDescriptionControl, new GUIContent("Description", "One line to remember what the flag is for."),
                detailEdit.Description, _value => detailEdit.Description = _value);

            if (_playing)
            {
                bool _current = Flags.GetValue(_flag.Guid);
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("Current Value", _current ? "true" : "false");
                    if (GUILayout.Button("Toggle Now", GUILayout.Width(90f))) SetValue(_database, _flag, !_current, true);
                }
            }

            if (rows.TryGetValue(_flag.Guid, out RowInfo _info))
                foreach (string _warning in _info.Warnings) EditorGUILayout.HelpBox(_warning, MessageType.Warning);

            if (_flag.Scope == FlagScope.Local && string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(_flag.SceneGuid ?? string.Empty)))
                DrawMoveToScene(_database, _flag);

            EditorGUILayout.Space();
            DrawUses(_index, _flag);

            EditorGUILayout.Space();
            DrawHistory(_flag);

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Rename")) BeginRename(_flag);
                using (new EditorGUI.DisabledScope(_playing))
                    if (GUILayout.Button(new GUIContent("Delete", _playing ? PlayLockedTooltip : string.Empty))) Delete(_database, _flag);
            }

            EditorGUILayout.EndScrollView();
        }

        /// <summary>
        /// Keeps the Name/Description buffer tied to the selected flag. Leaving a detail field commits it to the flag it was typed for;
        /// while no field is being edited the buffer follows the database (undo, inline rename).
        /// </summary>
        private void SyncDetailEdit(FlagDefinition _flag)
        {
            string _focused = GUI.GetNameOfFocusedControl();
            bool _editing = _focused == DetailNameControl || _focused == DetailDescriptionControl;

            if (detailWasFocused && !_editing) CommitDetailEdit();
            detailWasFocused = _editing;
            if (!_editing || !detailEdit.IsFor(_flag.Guid)) detailEdit.Load(_flag);
        }

        /// <summary>Text field bound to the detail buffer. Enter commits it, Esc drops the pending text.</summary>
        private void DrawDetailField(FlagDefinition _flag, string _control, GUIContent _label, string _value, Action<string> _set)
        {
            Event _event = Event.current;
            if (_event.type == EventType.KeyDown && GUI.GetNameOfFocusedControl() == _control)
            {
                if (_event.keyCode == KeyCode.Return || _event.keyCode == KeyCode.KeypadEnter)
                {
                    CommitDetailEdit();
                    _event.Use();
                }
                else if (_event.keyCode == KeyCode.Escape)
                {
                    detailEdit.Load(_flag);
                    detailError = null;
                    GUI.FocusControl(null);
                    _event.Use();
                }
            }

            GUI.SetNextControlName(_control);
            _set(EditorGUILayout.TextField(_label, _value));
        }

        /// <summary>Writes the pending detail edits to the flag they were typed for, whichever flag is selected now.</summary>
        private void CommitDetailEdit()
        {
            FlagDatabase _database = FlagDatabaseLocator.Find();
            if (_database == null || string.IsNullOrEmpty(detailEdit.Guid)) return;

            bool _renamed = detailEdit.Commit(_database, out string _error);
            detailError = _renamed ? null : _error;
            MarkDirty();
        }

        private void DrawMoveToScene(FlagDatabase _database, FlagDefinition _flag)
        {
            GUIContent _button = new GUIContent("Move to scene…", $"'{_flag.SceneName}' no longer exists. Give the flag to another scene of the project.");
            if (!GUILayout.Button(_button)) return;

            GenericMenu _menu = new GenericMenu();
            foreach (SceneOption _scene in projectScenes)
            {
                string _guid = _scene.Guid;
                _menu.AddItem(new GUIContent(_scene.Name), false, () =>
                {
                    if (FlagDatabaseEditing.TryMoveToScene(_database, _flag.Guid, _guid, out string _error)) detailError = null;
                    else detailError = _error;
                    MarkDirty();
                });
            }
            if (projectScenes.Count == 0) _menu.AddDisabledItem(new GUIContent("No scenes in the project"));
            _menu.ShowAsContext();
        }

        private void DrawUses(FlagUsageIndex _index, FlagDefinition _flag)
        {
            EditorGUILayout.LabelField("Uses", EditorStyles.boldLabel);
            if (_index == null)
            {
                GUILayout.Label("No usage index yet. Press Rebuild Usage Index.", mutedStyle);
                return;
            }

            List<FlagUsageEntry> _uses = _index.For(_flag.Guid).ToList();
            if (_uses.Count == 0)
            {
                GUILayout.Label("Not used anywhere.", mutedStyle);
                return;
            }

            DrawUsesOfKind(_uses, FlagUsageKind.Write);
            DrawUsesOfKind(_uses, FlagUsageKind.Read);
            DrawUsesOfKind(_uses, FlagUsageKind.Other);
        }

        private void DrawUsesOfKind(List<FlagUsageEntry> _uses, FlagUsageKind _kind)
        {
            List<FlagUsageEntry> _ofKind = _uses.Where(_use => _use.kind == _kind).ToList();
            if (_ofKind.Count == 0) return;

            GUILayout.Label(KindLabel(_kind), mutedStyle);
            foreach (FlagUsageEntry _use in _ofKind)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(12f);
                    if (GUILayout.Button(new GUIContent(UsageLabel(_use), ComponentLabel(_use)), linkStyle))
                        RevealUsage(_use);
                    EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(), MouseCursor.Link);
                }
            }
        }

        private void DrawHistory(FlagDefinition _flag)
        {
            EditorGUILayout.LabelField("Play Session History", EditorStyles.boldLabel);
            List<FlagHistoryEntry> _entries = FlagHistory.For(_flag.Guid).ToList();
            if (_entries.Count == 0)
            {
                GUILayout.Label(EditorApplication.isPlaying ? "No changes or reads yet." : "Enter Play to record changes and reads.", mutedStyle);
                return;
            }

            foreach (FlagHistoryEntry _entry in _entries)
            {
                string _what = _entry.IsRead
                    ? $"read = {Bool(_entry.NewValue)}" + (_entry.Count > 1 ? $" (×{_entry.Count})" : string.Empty)
                    : $"{Bool(_entry.OldValue)} → {Bool(_entry.NewValue)}" + (_entry.Reason != FlagChangeReason.Live ? $" ({_entry.Reason})" : string.Empty);
                GUILayout.Label($"{Clock(_entry.Time)}   {_what}   {_entry.SourceName}", historyStyle);
            }
            GUILayout.Label($"{Clock(playStartTime)}  {Bool(_flag.InitialValue)} (initial value)", historyStyle);
        }

        // ==================== NAVIGATION ====================

        /// <summary>Selects and pings the object or asset of a use. A closed scene is opened additively after asking.</summary>
        private void RevealUsage(FlagUsageEntry _use)
        {
            if (!string.IsNullOrEmpty(_use.assetPath))
            {
                UnityEngine.Object _asset = AssetDatabase.LoadMainAssetAtPath(_use.assetPath);
                if (_asset == null)
                {
                    ShowNotification(new GUIContent("The asset no longer exists. Rebuild the usage index."));
                    return;
                }
                Selection.activeObject = _asset;
                EditorGUIUtility.PingObject(_asset);
                return;
            }

            string _scenePath = AssetDatabase.GUIDToAssetPath(_use.sceneGuid);
            if (string.IsNullOrEmpty(_scenePath))
            {
                ShowNotification(new GUIContent("The scene of this use no longer exists."));
                return;
            }

            if (_use.approximate || string.IsNullOrEmpty(_use.hierarchyPath))
            {
                EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(_scenePath));
                ShowNotification(new GUIContent("Rebuild the usage index to locate this use."));
                return;
            }

            Scene _scene = SceneManager.GetSceneByPath(_scenePath);
            if (!_scene.isLoaded)
            {
                string _sceneName = Path.GetFileNameWithoutExtension(_scenePath);
                if (EditorApplication.isPlaying)
                {
                    ShowNotification(new GUIContent($"'{_sceneName}' is not loaded. Leave Play to open it."));
                    return;
                }
                if (!EditorUtility.DisplayDialog("Open Scene", $"'{_sceneName}' is not open. Open it additively to select the object?", "Open", "Cancel")) return;
                _scene = EditorSceneManager.OpenScene(_scenePath, OpenSceneMode.Additive);
            }

            GameObject _target = FindByPath(_scene, _use.hierarchyPath);
            if (_target == null)
            {
                ShowNotification(new GUIContent("Object not found. Rebuild the usage index."));
                return;
            }
            Selection.activeGameObject = _target;
            EditorGUIUtility.PingObject(_target);
        }

        private static GameObject FindByPath(Scene _scene, string _hierarchyPath)
        {
            if (!_scene.IsValid() || !_scene.isLoaded) return null;

            int _slash = _hierarchyPath.IndexOf('/');
            string _rootName = _slash < 0 ? _hierarchyPath : _hierarchyPath.Substring(0, _slash);
            string _rest = _slash < 0 ? null : _hierarchyPath.Substring(_slash + 1);

            foreach (GameObject _root in _scene.GetRootGameObjects())
            {
                if (_root.name != _rootName) continue;
                if (_rest == null) return _root;
                Transform _child = _root.transform.Find(_rest);
                if (_child != null) return _child.gameObject;
            }
            return null;
        }

        // ==================== KEYBOARD ====================

        private void HandleKeyboard(FlagDatabase _database)
        {
            Event _event = Event.current;
            if (_event.type != EventType.KeyDown || _database == null || EditorGUIUtility.editingTextField) return;
            if (string.IsNullOrEmpty(selectedGuid) || !_database.TryGet(selectedGuid, out FlagDefinition _flag)) return;

            if (_event.keyCode == KeyCode.F2)
            {
                BeginRename(_flag);
                _event.Use();
            }
            else if (_event.keyCode == KeyCode.Delete && !EditorApplication.isPlaying)
            {
                Delete(_database, _flag);
                _event.Use();
            }
        }

        // ==================== HELPERS ====================

        private void Select(string _guid)
        {
            if (selectedGuid != _guid)
            {
                if (renamingGuid != null && renamingGuid != _guid) CancelRename();

                // A detail edit still being typed belongs to the flag that was selected; save it there before switching.
                CommitDetailEdit();
                if (!string.IsNullOrEmpty(detailError)) ShowNotification(new GUIContent(detailError));
                detailEdit.Load(null);
                detailWasFocused = false;
                detailError = null;
                GUI.FocusControl(null);
            }
            selectedGuid = _guid;
            Repaint();
        }

        private void Rebuild(FlagDatabase _database, FlagUsageIndex _index)
        {
            dirty = false;
            lastFlagCount = _database != null ? _database.Flags.Count : -1;
            groups = FlagsWindowModel.Build(_database, search, filter);
            rows.Clear();
            if (_database == null) return;

            foreach (FlagGroup _group in groups)
            {
                foreach (FlagDefinition _flag in _group.Flags)
                {
                    rows[_flag.Guid] = new RowInfo
                    {
                        Uses = FlagsWindowModel.UsageCount(_index, _flag.Guid),
                        Warnings = FlagWarnings.For(_flag, _database, _index)
                    };
                }
            }
        }

        private void RefreshProjectScenes()
        {
            projectScenes = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" })
                .Select(_guid => new SceneOption { Guid = _guid, Name = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(_guid)) })
                .OrderBy(_scene => _scene.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>Scene picked for "+ Local": the one chosen in the toolbar, or the active scene when it is saved.</summary>
        private string LocalSceneGuid()
        {
            if (!string.IsNullOrEmpty(localSceneGuid) && projectScenes.Any(_scene => _scene.Guid == localSceneGuid)) return localSceneGuid;
            string _active = SceneManager.GetActiveScene().path;
            string _guid = string.IsNullOrEmpty(_active) ? string.Empty : AssetDatabase.AssetPathToGUID(_active);
            if (projectScenes.Any(_scene => _scene.Guid == _guid)) return _guid;
            return projectScenes.Count > 0 ? projectScenes[0].Guid : string.Empty;
        }

        private void DrawScenePopup(string _current, Action<string> _onPicked, GUIStyle _style, float _width)
        {
            if (projectScenes.Count == 0)
            {
                GUILayout.Label("(no saved scenes)", mutedStyle, GUILayout.Width(_width));
                return;
            }

            int _index = Mathf.Max(0, projectScenes.FindIndex(_scene => _scene.Guid == _current));
            int _picked = EditorGUILayout.Popup(_index, projectScenes.Select(_scene => _scene.Name).ToArray(), _style, GUILayout.Width(_width));
            if (_picked != _index || string.IsNullOrEmpty(_current)) _onPicked(projectScenes[_picked].Guid);
        }

        private static string KindLabel(FlagUsageKind _kind) =>
            _kind == FlagUsageKind.Write ? "Sets" : _kind == FlagUsageKind.Read ? "Reads" : "Other";

        private static string UsageLabel(FlagUsageEntry _use)
        {
            if (!string.IsNullOrEmpty(_use.assetPath))
            {
                string _asset = Path.GetFileName(_use.assetPath);
                return string.IsNullOrEmpty(_use.hierarchyPath) || _use.hierarchyPath == Path.GetFileNameWithoutExtension(_use.assetPath)
                    ? _asset : _use.hierarchyPath + " (" + _asset + ")";
            }

            string _scenePath = AssetDatabase.GUIDToAssetPath(_use.sceneGuid);
            string _scene = string.IsNullOrEmpty(_scenePath) ? "missing scene" : Path.GetFileNameWithoutExtension(_scenePath);
            string _where = _use.approximate || string.IsNullOrEmpty(_use.hierarchyPath) ? "somewhere (rebuild to locate)" : _use.hierarchyPath;
            return _where + " (" + _scene + ")";
        }

        private static string ComponentLabel(FlagUsageEntry _use)
        {
            if (string.IsNullOrEmpty(_use.componentType)) return "Click to select.";
            int _dot = _use.componentType.LastIndexOf('.');
            return (_dot >= 0 ? _use.componentType.Substring(_dot + 1) : _use.componentType) + ". Click to select.";
        }

        private static string Ago(double _seconds)
        {
            if (_seconds < 60) return Mathf.FloorToInt((float)_seconds) + "s ago";
            if (_seconds < 3600) return Mathf.FloorToInt((float)(_seconds / 60)) + "m ago";
            return Mathf.FloorToInt((float)(_seconds / 3600)) + "h ago";
        }

        private string Clock(double _time)
        {
            double _seconds = Math.Max(0, _time - playStartTime);
            int _minutes = (int)(_seconds / 60);
            return _minutes.ToString("00", CultureInfo.InvariantCulture) + ":" + (_seconds - _minutes * 60).ToString("00.0", CultureInfo.InvariantCulture);
        }

        private static string Bool(bool _value) => _value ? "true" : "false";

        private void EnsureStyles()
        {
            if (mutedStyle != null) return;

            mutedStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleLeft };
            mutedStyle.normal.textColor = EditorGUIUtility.isProSkin ? new Color(0.6f, 0.6f, 0.6f) : new Color(0.35f, 0.35f, 0.35f);

            warningStyle = new GUIStyle(mutedStyle);
            warningStyle.normal.textColor = WarningColor;

            errorStyle = new GUIStyle(mutedStyle) { wordWrap = true };
            errorStyle.normal.textColor = ErrorColor;

            rowErrorStyle = new GUIStyle(mutedStyle) { clipping = TextClipping.Clip };
            rowErrorStyle.normal.textColor = ErrorColor;

            linkStyle = new GUIStyle(EditorStyles.linkLabel) { wordWrap = true };

            historyStyle = new GUIStyle(EditorStyles.miniLabel);
        }
    }
}
