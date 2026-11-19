using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarryTools.Dialogue.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditor.Localization;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace HarryTools.Dialogue.Tests {
    public class DialogueGraphWindowTests {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string TempFolder = "Assets/__DialogueTests__";
        private const string AssetsFolder = TempFolder + "/GraphWindow";
        private const string TablesFolder = TempFolder + "/GraphWindowTables";
        private const string LocalesFolder = TempFolder + "/GraphWindowLocales";
        private const string SourceCode = "en";
        private const string OtherCode = "es";
        private const string Ellipsis = "…";
        private const int MaxLayoutFrames = 200;
        // Pixels a port connector may sit inside the node's border
        private const float MaxPortInset = 4f;

        private readonly List<Locale> addedLocales = new List<Locale>();
        private LocalizationSettings createdSettings;
        private DialogueSettings settings;
        private DialogueGraphWindow window;
        private bool runsStarted;
        #endregion

        // ==================== SETUP ====================
        #region Setup
        [OneTimeSetUp]
        public void OneTimeSetUp() {
            EnsureFolder(TempFolder);

            // Reuse the project's localization setup; add only what is missing and undo it afterwards
            if (LocalizationEditorSettings.ActiveLocalizationSettings == null) {
                createdSettings = ScriptableObject.CreateInstance<LocalizationSettings>();
                AssetDatabase.CreateAsset(createdSettings, TempFolder + "/GraphWindowLocalization.asset");
                LocalizationEditorSettings.ActiveLocalizationSettings = createdSettings;
            }
            EnsureLocale(SourceCode);
            EnsureLocale(OtherCode);
        }

        private void EnsureLocale(string _code) {
            if (LocalizationEditorSettings.GetLocale(_code) != null) return;
            EnsureFolder(LocalesFolder);
            Locale _locale = Locale.CreateLocale(_code);
            AssetDatabase.CreateAsset(_locale, LocalesFolder + "/" + _code + ".asset");
            DialogueLocaleWatcher.AddLocaleForTests(_locale);
            addedLocales.Add(_locale);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown() {
            for (int _i = 0; _i < addedLocales.Count; _i++) {
                if (addedLocales[_i] != null) LocalizationEditorSettings.RemoveLocale(addedLocales[_i]);
            }
            addedLocales.Clear();
            if (createdSettings != null) LocalizationEditorSettings.ActiveLocalizationSettings = null;
            createdSettings = null;

            if (AssetDatabase.IsValidFolder(LocalesFolder)) AssetDatabase.DeleteAsset(LocalesFolder);
            if (AssetDatabase.IsValidFolder(TempFolder) && AssetDatabase.FindAssets("", new[] { TempFolder }).Length == 0) AssetDatabase.DeleteAsset(TempFolder);
        }

        [SetUp]
        public void SetUp() {
            settings = ScriptableObject.CreateInstance<DialogueSettings>();
            settings.TablesFolder = TablesFolder;
            settings.SourceLocaleCode = SourceCode;
            DialogueSettings.UseForTests(settings);
            EnsureFolder(TempFolder);
            EnsureFolder(AssetsFolder);
            Undo.IncrementCurrentGroup();
        }

        [TearDown]
        public void TearDown() {
            if (window != null) window.Close();
            window = null;
            if (runsStarted) {
                Dialogue.StopAll();
                Dialogue.ClearForTests();
                DialogueMemory.ClearForTests();
                runsStarted = false;
            }

            DialogueSettings.ClearForTests();
            Object.DestroyImmediate(settings);

            DeleteTables();
            if (AssetDatabase.IsValidFolder(AssetsFolder)) AssetDatabase.DeleteAsset(AssetsFolder);
            if (AssetDatabase.IsValidFolder(TempFolder) && AssetDatabase.FindAssets("", new[] { TempFolder }).Length == 0) AssetDatabase.DeleteAsset(TempFolder);
            Undo.ClearAll();
        }

        private static void EnsureFolder(string _path) {
            if (AssetDatabase.IsValidFolder(_path)) return;
            int _slash = _path.LastIndexOf('/');
            AssetDatabase.CreateFolder(_path.Substring(0, _slash), _path.Substring(_slash + 1));
        }

        private static void DeleteTables() {
            if (!AssetDatabase.IsValidFolder(TablesFolder)) return;
            // Collections first: deleting one removes it from the project before its tables go
            foreach (string _guid in AssetDatabase.FindAssets("t:StringTableCollection", new[] { TablesFolder })) {
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(_guid));
            }
            AssetDatabase.DeleteAsset(TablesFolder);
        }

        private static DialogueAsset NewAsset(string _name) {
            DialogueAsset _asset = ScriptableObject.CreateInstance<DialogueAsset>();
            _asset.EnsureIds();
            AssetDatabase.CreateAsset(_asset, AssetsFolder + "/" + _name + ".asset");
            return _asset;
        }

        /// <summary> Start → line → choice with two options; the first option → end; plus a loose branch with one case </summary>
        private static DialogueAsset NewSample(string _name) {
            DialogueAsset _asset = NewAsset(_name);
            DialogueNode _line = DialogueGraphOps.CreateConnected(_asset, typeof(LineNode), new Vector2(200f, 0f), _asset.Start.Next.Id);
            ChoiceNode _choice = (ChoiceNode)DialogueGraphOps.CreateConnected(_asset, typeof(ChoiceNode), new Vector2(400f, 0f), ((LineNode)_line).Next.Id);
            DialogueGraphOps.AddOption(_asset, _choice);
            DialogueGraphOps.AddOption(_asset, _choice);
            DialogueGraphOps.CreateConnected(_asset, typeof(EndNode), new Vector2(600f, 0f), _choice.Options[0].Next.Id);
            BranchNode _branch = (BranchNode)DialogueGraphOps.CreateNode(_asset, typeof(BranchNode), new Vector2(0f, 300f));
            DialogueGraphOps.AddCase(_asset, _branch);
            Undo.ClearAll();
            return _asset;
        }

        /// <summary> Every non null node has one view, every output one port, and every connected output exactly one edge to its target </summary>
        private static void AssertViewMatchesAsset(DialogueGraphView _view, DialogueAsset _asset) {
            List<DialogueNode> _nodes = _asset.Nodes.Where(_node => _node != null).ToList();
            List<DialogueNodeView> _views = _view.nodes.OfType<DialogueNodeView>().ToList();
            Assert.AreEqual(_nodes.Count, _views.Count, "node views");

            int _connected = 0;
            List<NodeOutput> _outputs = new List<NodeOutput>();
            for (int _i = 0; _i < _nodes.Count; _i++) {
                DialogueNodeView _nodeView = _view.FindNodeView(_nodes[_i].Id);
                Assert.IsNotNull(_nodeView, "view of " + _nodes[_i].Id);
                Assert.AreEqual(_nodes[_i].Id, _nodeView.NodeId);

                _outputs.Clear();
                _nodes[_i].CollectOutputs(_outputs);
                for (int _o = 0; _o < _outputs.Count; _o++) {
                    Port _port = _nodeView.Outputs.FirstOrDefault(_pair => _pair.OutputId == _outputs[_o].Id).Port;
                    // A jump that continues in another dialogue has no output on the canvas
                    if (_nodes[_i] is JumpNode _jump && _jump.Mode == JumpMode.Continue) {
                        Assert.IsNull(_port);
                        continue;
                    }
                    Assert.IsNotNull(_port, "port of " + _outputs[_o].Id);
                    if (!_outputs[_o].IsConnected) {
                        Assert.AreEqual(0, _port.connections.Count());
                        continue;
                    }
                    _connected++;
                    Assert.AreEqual(1, _port.connections.Count());
                    Assert.AreSame(_view.FindNodeView(_outputs[_o].Target).Input, _port.connections.First().input);
                }
            }
            Assert.AreEqual(_connected, _view.edges.Count(), "edges");
        }
        #endregion

        // ==================== TESTS ====================
        #region Tests
        [Test]
        public void Window_Open_BuildsOneViewPerNode_AndEdges() {
            DialogueAsset _asset = NewSample("Open");

            window = DialogueGraphWindow.Open(_asset);

            Assert.AreSame(_asset, window.Asset);
            Assert.AreEqual(5, _asset.Nodes.Count);
            AssertViewMatchesAsset(window.View, _asset);

            ChoiceNode _choice = _asset.Nodes.OfType<ChoiceNode>().First();
            DialogueNodeView _choiceView = window.View.FindNodeView(_choice.Id);
            // Two options plus no answer
            Assert.AreEqual(3, _choiceView.Outputs.Count);
            Assert.AreEqual(_choice.NoAnswer.Id, _choiceView.Outputs[2].OutputId);
            Assert.AreEqual(Port.Capacity.Single, _choiceView.Outputs[0].Port.capacity);
            Assert.AreEqual(Port.Capacity.Multi, _choiceView.Input.capacity);
            Assert.AreEqual(Orientation.Horizontal, _choiceView.Input.orientation);

            BranchNode _branch = _asset.Nodes.OfType<BranchNode>().First();
            // One case plus otherwise
            Assert.AreEqual(2, window.View.FindNodeView(_branch.Id).Outputs.Count);

            DialogueNodeView _startView = window.View.FindNodeView(_asset.Start.Id);
            Assert.IsNull(_startView.Input);
            Assert.AreEqual((Capabilities)0, _startView.capabilities & (Capabilities.Deletable | Capabilities.Copiable));
            Assert.AreEqual(_asset.Start.Position, _startView.CanvasPosition);
        }

        [Test]
        public void Window_JumpOutput_OnlyWhenCallAndReturn() {
            DialogueAsset _asset = NewAsset("Jump");
            JumpNode _jump = (JumpNode)DialogueGraphOps.CreateNode(_asset, typeof(JumpNode), Vector2.zero);

            window = DialogueGraphWindow.Open(_asset);
            Assert.AreEqual(0, window.View.FindNodeView(_jump.Id).Outputs.Count);

            _jump.Mode = JumpMode.CallAndReturn;
            window.View.Rebuild();
            Assert.AreEqual(1, window.View.FindNodeView(_jump.Id).Outputs.Count);
        }

        [Test]
        public void Window_CreateViaOps_Rebuilds() {
            DialogueAsset _asset = NewAsset("Create");
            window = DialogueGraphWindow.Open(_asset);

            DialogueNode _line = window.View.CreateNode(typeof(LineNode), new Vector2(120f, 40f), _asset.Start.Next.Id);
            DialogueNode _end = window.View.CreateNode(typeof(EndNode), new Vector2(300f, 40f), null);

            Assert.IsNotNull(_line);
            Assert.AreEqual(_line.Id, _asset.Start.Next.Target);
            Assert.AreEqual(new Vector2(120f, 40f), window.View.FindNodeView(_line.Id).CanvasPosition);
            Assert.IsNotNull(window.View.FindNodeView(_end.Id));
            AssertViewMatchesAsset(window.View, _asset);

            window.View.Connect(((LineNode)_line).Next.Id, _end.Id);
            Assert.AreEqual(_end.Id, ((LineNode)_line).Next.Target);
            AssertViewMatchesAsset(window.View, _asset);
        }

        [Test]
        public void Window_Move_WritesPosition_SnappedToGrid() {
            DialogueAsset _asset = NewSample("Move");
            window = DialogueGraphWindow.Open(_asset);
            DialogueNode _line = _asset.Nodes.OfType<LineNode>().First();
            DialogueNodeView _view = window.View.FindNodeView(_line.Id);
            window.View.SnapToGrid = true;

            _view.SetPosition(new Rect(133f, 47f, 0f, 0f));
            window.View.graphViewChanged(new GraphViewChange { movedElements = new List<GraphElement> { _view } });

            Assert.AreEqual(new Vector2(140f, 40f), _line.Position);
            Assert.AreEqual(new Vector2(140f, 40f), _view.CanvasPosition);
        }

        [Test]
        public void Window_DeleteSelection_KeepsStart() {
            DialogueAsset _asset = NewSample("DeleteSelection");
            window = DialogueGraphWindow.Open(_asset);
            DialogueNode _line = _asset.Nodes.OfType<LineNode>().First();

            window.View.AddToSelection(window.View.FindNodeView(_asset.Start.Id));
            window.View.AddToSelection(window.View.FindNodeView(_line.Id));
            window.View.DeleteSelection();

            Assert.IsNotNull(_asset.Start);
            Assert.IsNull(_asset.Find(_line.Id));
            Assert.IsFalse(_asset.Start.Next.IsConnected);
            AssertViewMatchesAsset(window.View, _asset);
        }

        [Test]
        public void Undo_AfterOps_ViewMatchesAsset() {
            DialogueAsset _asset = NewSample("Undo");
            window = DialogueGraphWindow.Open(_asset);
            DialogueGraphView _view = window.View;
            List<string> _states = new List<string> { EditorJsonUtility.ToJson(_asset) };

            // Create
            Undo.IncrementCurrentGroup();
            DialogueNode _created = _view.CreateNode(typeof(LineNode), new Vector2(0f, 600f), null);
            _states.Add(EditorJsonUtility.ToJson(_asset));
            AssertViewMatchesAsset(_view, _asset);

            // Connect: the branch otherwise → the new line
            Undo.IncrementCurrentGroup();
            BranchNode _branch = _asset.Nodes.OfType<BranchNode>().First();
            _view.Connect(_branch.Otherwise.Id, _created.Id);
            _states.Add(EditorJsonUtility.ToJson(_asset));
            AssertViewMatchesAsset(_view, _asset);

            // Delete through the canvas: the choice and its edges
            Undo.IncrementCurrentGroup();
            ChoiceNode _choice = _asset.Nodes.OfType<ChoiceNode>().First();
            _view.DeleteElements(new GraphElement[] { _view.FindNodeView(_choice.Id) });
            Assert.IsNull(_asset.Find(_choice.Id));
            _states.Add(EditorJsonUtility.ToJson(_asset));
            AssertViewMatchesAsset(_view, _asset);

            // Paste a copy of the branch and the line it points to
            Undo.IncrementCurrentGroup();
            string _data = DialogueClipboard.Copy(_asset, new[] { _branch.Id, _created.Id });
            DialogueClipboard.Paste(_asset, _data, new Vector2(50f, 50f));
            _view.Rebuild();
            _states.Add(EditorJsonUtility.ToJson(_asset));
            AssertViewMatchesAsset(_view, _asset);

            for (int _i = _states.Count - 2; _i >= 0; _i--) {
                Undo.PerformUndo();
                Assert.AreEqual(_states[_i], EditorJsonUtility.ToJson(_asset), "undo to state " + _i);
                AssertViewMatchesAsset(_view, _asset);
            }
            for (int _i = 1; _i < _states.Count; _i++) {
                Undo.PerformRedo();
                Assert.AreEqual(_states[_i], EditorJsonUtility.ToJson(_asset), "redo to state " + _i);
                AssertViewMatchesAsset(_view, _asset);
            }
        }

        [Test]
        public void Window_NullNode_Opens() {
            DialogueAsset _asset = NewSample("NullNode");
            _asset.NodeList.Insert(1, null);

            window = DialogueGraphWindow.Open(_asset);

            Assert.AreEqual(5, window.View.nodes.Count());
            AssertViewMatchesAsset(window.View, _asset);
        }

        [UnityTest]
        public IEnumerator Window_AssetDeleted_ShowsEmpty() {
            DialogueAsset _asset = NewSample("Deleted");
            window = DialogueGraphWindow.Open(_asset);
            Assert.AreEqual(5, window.View.nodes.Count());

            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(_asset));
            for (int _i = 0; _i < 5; _i++) yield return null;

            Assert.IsTrue(window.Asset == null);
            Assert.AreEqual(0, window.View.nodes.Count());
            Assert.AreEqual(0, window.View.edges.Count());

            // Undo and redo with nothing open must not throw
            Undo.PerformUndo();
            Undo.PerformRedo();
            Assert.AreEqual(0, window.View.nodes.Count());
        }

        [Test]
        public void Window_DomainReload_ReopensAsset() {
            DialogueAsset _asset = NewSample("Reload");
            window = DialogueGraphWindow.Open(_asset);
            DialogueGraphView _before = window.View;

            // A domain reload disables the window, drops everything but its serialized fields, then enables it again
            window.OnDisable();
            window.OnEnable();

            Assert.AreSame(_asset, window.Asset);
            Assert.AreNotSame(_before, window.View);
            AssertViewMatchesAsset(window.View, _asset);
        }

        [UnityTest]
        public IEnumerator Panel_ShowsSelectedNode_ChangesApplyToAsset() {
            DialogueAsset _asset = NewSample("Panel");
            window = DialogueGraphWindow.Open(_asset);
            LineNode _line = _asset.Nodes.OfType<LineNode>().First();

            // Nothing selected: the dialogue options
            Assert.IsNull(window.Panel.NodeId);
            Assert.IsNotNull(FindBoundField(window.Panel, "allowSkip"));

            window.View.AddToSelection(window.View.FindNodeView(_line.Id));
            Assert.AreEqual(_line.Id, window.Panel.NodeId);
            // Property fields create their inner field once bound
            for (int _i = 0; _i < 200 && FindBoundField(window.Panel, "delayBefore")?.Q<FloatField>() == null; _i++) yield return null;

            FloatField _delay = FindBoundField(window.Panel, "delayBefore")?.Q<FloatField>();
            Assert.IsNotNull(_delay, "delay field of the line");
            _delay.value = 2f;
            LineNodeView LineView() => (LineNodeView)window.View.FindNodeView(_line.Id);
            for (int _i = 0; _i < 200 && !LineView().Indicators.Contains("Delay"); _i++) yield return null;

            Assert.AreEqual(2f, _line.DelayBefore);
            CollectionAssert.Contains(LineView().Indicators, "Delay");

            window.View.ClearSelection();
            Assert.IsNull(window.Panel.NodeId);
        }

        [Test]
        public void Panel_AddOption_RebuildsPorts() {
            DialogueAsset _asset = NewSample("PanelOptions");
            window = DialogueGraphWindow.Open(_asset);
            ChoiceNode _choice = _asset.Nodes.OfType<ChoiceNode>().First();
            window.View.AddToSelection(window.View.FindNodeView(_choice.Id));
            Assert.AreEqual(2, window.Panel.ItemCount);

            Undo.IncrementCurrentGroup();
            window.Panel.AddItem();

            Assert.AreEqual(3, _choice.Options.Count);
            Assert.AreEqual(4, window.View.FindNodeView(_choice.Id).Outputs.Count, "three options plus no answer");
            Assert.AreEqual(3, window.Panel.ItemCount);
            Assert.AreEqual(_choice.Id, window.Panel.NodeId);
            AssertViewMatchesAsset(window.View, _asset);

            string _first = _choice.Options[0].Id;
            Undo.IncrementCurrentGroup();
            window.Panel.MoveItem(0, 2);
            Assert.AreEqual(_first, _choice.Options[2].Id);
            Assert.AreEqual(_choice.Options[2].Next.Id, window.View.FindNodeView(_choice.Id).Outputs[2].OutputId);

            Undo.IncrementCurrentGroup();
            window.Panel.RemoveItem(2);
            Assert.AreEqual(2, _choice.Options.Count);
            Assert.AreEqual(3, window.View.FindNodeView(_choice.Id).Outputs.Count);
            Assert.AreEqual(2, window.Panel.ItemCount);
            AssertViewMatchesAsset(window.View, _asset);

            BranchNode _branch = _asset.Nodes.OfType<BranchNode>().First();
            window.View.ClearSelection();
            window.View.AddToSelection(window.View.FindNodeView(_branch.Id));
            window.Panel.AddItem();
            Assert.AreEqual(2, _branch.Cases.Count);
            Assert.AreEqual(3, window.View.FindNodeView(_branch.Id).Outputs.Count, "two cases plus otherwise");
        }

        [Test]
        public void Canvas_SelfConnection_Allowed() {
            DialogueAsset _asset = NewSample("SelfLoop");
            window = DialogueGraphWindow.Open(_asset);
            DialogueNodeView _line = window.View.FindNodeView(_asset.Nodes.OfType<LineNode>().First().Id);

            List<Port> _compatible = window.View.GetCompatiblePorts(_line.Outputs[0].Port, null);

            CollectionAssert.Contains(_compatible, _line.Input);
            CollectionAssert.DoesNotContain(_compatible, _line.Outputs[0].Port);
        }

        [UnityTest]
        public IEnumerator ScreenToCanvas_RoundTripsThroughThePanel() {
            DialogueAsset _asset = NewSample("Screen");
            window = DialogueGraphWindow.Open(_asset);
            DialogueGraphView _view = window.View;
            // The window's content sits below its tab only once laid out
            for (int _i = 0; _i < 100 && float.IsNaN(window.rootVisualElement.worldBound.height); _i++) yield return null;
            Assert.IsFalse(float.IsNaN(window.rootVisualElement.worldBound.height), "window laid out");

            // A point of the canvas, seen from the screen, maps back to the same canvas point
            Vector2 _canvas = new Vector2(123f, 45f);
            Vector2 _panel = _view.contentViewContainer.LocalToWorld(_canvas);
            Vector2 _screen = window.rootVisualElement.WorldToLocal(_panel) + window.position.position;

            Vector2 _back = _view.ScreenToCanvas(_screen);
            Assert.AreEqual(_canvas.x, _back.x, 0.01f);
            Assert.AreEqual(_canvas.y, _back.y, 0.01f);
        }

        [Test]
        public void Window_GroupFromSelection_SavedInAsset() {
            DialogueAsset _asset = NewSample("Group");
            window = DialogueGraphWindow.Open(_asset);
            LineNode _line = _asset.Nodes.OfType<LineNode>().First();
            ChoiceNode _choice = _asset.Nodes.OfType<ChoiceNode>().First();
            window.View.AddToSelection(window.View.FindNodeView(_line.Id));
            window.View.AddToSelection(window.View.FindNodeView(_choice.Id));

            Undo.IncrementCurrentGroup();
            window.View.GroupSelection();

            Assert.AreEqual(1, _asset.Groups.Count);
            DialogueGroup _group = _asset.Groups[0];
            CollectionAssert.AreEquivalent(new[] { _line.Id, _choice.Id }, _group.NodeIds);
            DialogueGroupView _groupView = window.View.FindGroupView(_group.Id);
            Assert.IsNotNull(_groupView);
            CollectionAssert.AreEquivalent(new GraphElement[] { window.View.FindNodeView(_line.Id), window.View.FindNodeView(_choice.Id) }, _groupView.containedElements);
            CollectionAssert.Contains(window.View.selection, _groupView, "the new group is selected");

            // Title and color are written to the asset
            Undo.IncrementCurrentGroup();
            _groupView.title = "Renamed";
            _groupView.SetColor(DialogueGroupView.Palette[1].Color);
            Assert.AreEqual("Renamed", _group.Title);
            Assert.AreEqual(DialogueGroupView.Palette[1].Color, _group.Color);

            // A node taken out of the group leaves it in the asset, and the rebuilt group keeps the rest
            Undo.IncrementCurrentGroup();
            _groupView.RemoveElement(window.View.FindNodeView(_choice.Id));
            CollectionAssert.AreEqual(new[] { _line.Id }, _group.NodeIds);
            window.View.Rebuild();
            CollectionAssert.AreEquivalent(new GraphElement[] { window.View.FindNodeView(_line.Id) }, window.View.FindGroupView(_group.Id).containedElements);
            Assert.AreEqual("Renamed", window.View.FindGroupView(_group.Id).title);

            // A node dropped into the group joins it
            Undo.IncrementCurrentGroup();
            window.View.FindGroupView(_group.Id).AddElement(window.View.FindNodeView(_choice.Id));
            CollectionAssert.AreEquivalent(new[] { _line.Id, _choice.Id }, _group.NodeIds);

            Undo.PerformUndo();
            Undo.PerformUndo();
            Undo.PerformUndo();
            Undo.PerformUndo();
            Assert.AreEqual(0, _asset.Groups.Count);
            Assert.AreEqual(0, window.View.graphElements.OfType<DialogueGroupView>().Count());
        }

        [Test]
        public void Window_Note_CreateEditMove() {
            DialogueAsset _asset = NewSample("Note");
            window = DialogueGraphWindow.Open(_asset);

            DialogueNote _note = window.View.CreateNote(new Vector2(100f, 50f));

            Assert.AreEqual(1, _asset.Notes.Count);
            Assert.AreEqual(new Vector2(100f, 50f), _note.Rect.position);
            DialogueNoteView _view = window.View.FindNoteView(_note.Id);
            Assert.IsNotNull(_view);

            _view.contents = "Remember this";
            using (StickyNoteChangeEvent _event = StickyNoteChangeEvent.GetPooled(_view, StickyNoteChange.Contents)) {
                _view.SendEvent(_event);
            }
            Assert.AreEqual("Remember this", _note.Text);

            _view.SetPosition(new Rect(300f, 20f, _note.Rect.width, _note.Rect.height));
            window.View.graphViewChanged(new GraphViewChange { movedElements = new List<GraphElement> { _view } });
            Assert.AreEqual(new Vector2(300f, 20f), _note.Rect.position);

            window.View.Rebuild();
            Assert.AreEqual("Remember this", window.View.FindNoteView(_note.Id).contents);
        }

        [Test]
        public void Window_CopyPaste_CreatesNewNodes() {
            {
                DialogueAsset _asset = NewSample("Copy");
                DialogueAsset _other = NewAsset("PasteTarget");
                window = DialogueGraphWindow.Open(_asset);
                window.View.UseTestClipboard();
                LineNode _line = _asset.Nodes.OfType<LineNode>().First();
                ChoiceNode _choice = _asset.Nodes.OfType<ChoiceNode>().First();
                window.View.AddToSelection(window.View.FindNodeView(_line.Id));
                window.View.AddToSelection(window.View.FindNodeView(_choice.Id));

                window.View.CopySelection();
                Undo.IncrementCurrentGroup();
                window.View.Paste();

                Assert.AreEqual(7, _asset.Nodes.Count);
                LineNode _newLine = _asset.Nodes.Skip(5).OfType<LineNode>().Single();
                ChoiceNode _newChoice = _asset.Nodes.Skip(5).OfType<ChoiceNode>().Single();
                Assert.AreNotEqual(_line.Id, _newLine.Id);
                Assert.AreNotEqual(_line.Position, _newLine.Position, "pasted away from the original");
                Assert.AreEqual(_newChoice.Id, _newLine.Next.Target, "link inside the copy kept");
                Assert.IsFalse(_newChoice.Options[0].Next.IsConnected, "link to a node not copied cleared");
                AssertViewMatchesAsset(window.View, _asset);
                CollectionAssert.AreEquivalent(new[] { _newLine.Id, _newChoice.Id }, window.View.selection.OfType<DialogueNodeView>().Select(_view => _view.NodeId), "the pasted nodes are selected");

                Undo.IncrementCurrentGroup();
                window.View.DuplicateSelection();
                Assert.AreEqual(9, _asset.Nodes.Count);
                AssertViewMatchesAsset(window.View, _asset);

                // Into another dialogue
                window.SetAsset(_other);
                Undo.IncrementCurrentGroup();
                window.View.Paste();
                Assert.AreEqual(3, _other.Nodes.Count);
                Assert.AreEqual(9, _asset.Nodes.Count);
                AssertViewMatchesAsset(window.View, _other);
            }
        }

        [Test]
        public void Window_OpenAllAtOrigin_LaysOutOnce() {
            DialogueAsset _asset = NewAsset("Layout");
            LineNode _line = (LineNode)DialogueGraphOps.CreateConnected(_asset, typeof(LineNode), Vector2.zero, _asset.Start.Next.Id);
            DialogueNode _loose = DialogueGraphOps.CreateNode(_asset, typeof(EndNode), Vector2.zero);
            Undo.ClearAll();

            window = DialogueGraphWindow.Open(_asset);

            Assert.AreEqual(new Vector2(350f, 0f), _line.Position);
            Assert.AreEqual(new Vector2(700f, 0f), _loose.Position);
            Assert.AreEqual(_line.Position, window.View.FindNodeView(_line.Id).CanvasPosition);

            Undo.PerformUndo();
            Assert.AreEqual(Vector2.zero, _line.Position);
            Assert.AreEqual(Vector2.zero, window.View.FindNodeView(_line.Id).CanvasPosition);

            // A dialogue already laid out is left as it is
            DialogueAsset _placed = NewSample("Placed");
            string _before = EditorJsonUtility.ToJson(_placed);
            window.SetAsset(_placed);
            Assert.AreEqual(_before, EditorJsonUtility.ToJson(_placed));
        }

        [Test]
        public void Canvas_ContextMenu_CreateNodeFirst() {
            DialogueAsset _asset = NewSample("Menu");
            window = DialogueGraphWindow.Open(_asset);
            DropdownMenu _menu = new DropdownMenu();

            window.View.BuildCanvasMenu(_menu, Vector2.zero);

            List<string> _names = _menu.MenuItems().OfType<DropdownMenuAction>().Select(_action => _action.name).ToList();
            Assert.AreEqual("Create Node", _names[0]);
            CollectionAssert.IsSubsetOf(new[] { "Paste", "Duplicate", "Auto Layout", "Create Group", "Create Note" }, _names);
        }

        [Test]
        public void Window_Finder_PickFramesAndSelects() {
            DialogueAsset _asset = NewSample("Finder");
            window = DialogueGraphWindow.Open(_asset);
            ChoiceNode _choice = _asset.Nodes.OfType<ChoiceNode>().First();

            window.Finder.Search("choice");

            Assert.AreEqual(1, window.Finder.Results.Count);
            window.Finder.Pick(0);
            CollectionAssert.AreEqual(new[] { _choice.Id }, window.View.selection.OfType<DialogueNodeView>().Select(_view => _view.NodeId));
        }

        [Test]
        public void Window_Regroup_RemovesEmptiedGroup() {
            DialogueAsset _asset = NewSample("Regroup");
            window = DialogueGraphWindow.Open(_asset);
            LineNode _line = _asset.Nodes.OfType<LineNode>().First();
            ChoiceNode _choice = _asset.Nodes.OfType<ChoiceNode>().First();
            void SelectBoth() {
                window.View.ClearSelection();
                window.View.AddToSelection(window.View.FindNodeView(_line.Id));
                window.View.AddToSelection(window.View.FindNodeView(_choice.Id));
            }

            SelectBoth();
            Undo.IncrementCurrentGroup();
            window.View.GroupSelection();
            string _first = _asset.Groups[0].Id;
            SelectBoth();
            Undo.IncrementCurrentGroup();
            window.View.GroupSelection();

            Assert.AreEqual(1, _asset.Groups.Count);
            Assert.AreNotEqual(_first, _asset.Groups[0].Id);
            Assert.AreEqual(1, window.View.graphElements.OfType<DialogueGroupView>().Count());

            // One undo step brings the first group back
            Undo.PerformUndo();
            Assert.AreEqual(1, _asset.Groups.Count);
            Assert.AreEqual(_first, _asset.Groups[0].Id);
            CollectionAssert.AreEquivalent(new[] { _line.Id, _choice.Id }, _asset.Groups[0].NodeIds);
        }

        [Test]
        public void Window_GroupShortcut_IgnoredInTextField() {
            DialogueAsset _asset = NewSample("Shortcut");
            window = DialogueGraphWindow.Open(_asset);
            DialogueNote _note = window.View.CreateNote(new Vector2(0f, 500f));
            TextField _noteField = window.View.FindNoteView(_note.Id).Q<TextField>();
            Assert.IsNotNull(_noteField, "the note has a text field");
            // After the note: creating it rebuilt the node views
            LineNodeView _lineView = (LineNodeView)window.View.FindNodeView(_asset.Nodes.OfType<LineNode>().First().Id);
            window.View.ClearSelection();
            window.View.AddToSelection(_lineView);
            void SendCtrlG(VisualElement _target) {
                using (KeyDownEvent _event = KeyDownEvent.GetPooled('\0', KeyCode.G, EventModifiers.Control)) {
                    _event.target = _target;
                    _target.SendEvent(_event);
                }
            }

            SendCtrlG(_noteField.Q<TextElement>());
            Assert.AreEqual(0, _asset.Groups.Count, "typing in a note field");

            SendCtrlG(_lineView);
            Assert.AreEqual(1, _asset.Groups.Count, "on the canvas");
        }

        [Test]
        public void Window_GroupColorPicker_OneUndoStep() {
            DialogueAsset _asset = NewSample("PickerUndo");
            window = DialogueGraphWindow.Open(_asset);
            window.View.AddToSelection(window.View.FindNodeView(_asset.Nodes.OfType<LineNode>().First().Id));
            window.View.GroupSelection();
            DialogueGroup _group = _asset.Groups[0];
            Color _original = _group.Color;
            DialogueGroupView _view = window.View.FindGroupView(_group.Id);

            // Dragging in the picker sends a change per event
            Undo.IncrementCurrentGroup();
            _view.ColorPicker.value = Color.red;
            Undo.IncrementCurrentGroup();
            _view.ColorPicker.value = Color.green;
            Undo.IncrementCurrentGroup();
            _view.ColorPicker.value = Color.blue;
            Assert.AreEqual(Color.blue, _group.Color);

            Undo.PerformUndo();
            Assert.AreEqual(_original, _asset.Groups[0].Color);
            Assert.AreEqual(1, _asset.Groups.Count, "the group itself is an earlier step");
        }

        [Test]
        public void Window_Arrange_SnapsToGrid() {
            DialogueAsset _asset = NewSample("ArrangeSnap");
            window = DialogueGraphWindow.Open(_asset);
            LineNode _line = _asset.Nodes.OfType<LineNode>().First();
            ChoiceNode _choice = _asset.Nodes.OfType<ChoiceNode>().First();
            _line.Position = new Vector2(133f, 47f);
            window.View.Rebuild();
            window.View.SnapToGrid = true;
            window.View.AddToSelection(window.View.FindNodeView(_line.Id));
            window.View.AddToSelection(window.View.FindNodeView(_choice.Id));

            window.View.ArrangeSelection(DialogueGraphOps.Arrangement.Left);

            Assert.AreEqual(new Vector2(140f, 40f), _line.Position);
            Assert.AreEqual(new Vector2(140f, 0f), _choice.Position);
        }

        [Test]
        public void Note_ContextMenu_HasEditItems() {
            DialogueAsset _asset = NewSample("NoteMenu");
            window = DialogueGraphWindow.Open(_asset);
            DialogueNote _note = window.View.CreateNote(Vector2.zero);
            window.View.ClearSelection();
            DropdownMenu _menu = new DropdownMenu();

            window.View.BuildElementMenu(_menu, window.View.FindNoteView(_note.Id).Q<Label>("contents"));

            List<DropdownMenuAction> _actions = _menu.MenuItems().OfType<DropdownMenuAction>().ToList();
            CollectionAssert.IsSubsetOf(new[] { "Copy", "Duplicate", "Delete" }, _actions.Select(_action => _action.name));
            Assert.IsTrue(window.View.selection.Contains(window.View.FindNoteView(_note.Id)), "the right clicked note is selected");

            DropdownMenuAction _delete = _actions.First(_action => _action.name == "Delete");
            _delete.UpdateActionStatus(null);
            Assert.AreEqual(DropdownMenuAction.Status.Normal, _delete.status);
            _delete.Execute();
            Assert.AreEqual(0, _asset.Notes.Count);
        }

        [Test]
        public void Window_IssuesListed_AfterChange() {
            DialogueAsset _asset = NewSample("IssuesListed");
            window = DialogueGraphWindow.Open(_asset);
            Assert.IsFalse(window.IssueList.Entries.Any(_entry => _entry.Text.Contains("Jump has no target")));

            JumpNode _jump = (JumpNode)DialogueGraphOps.CreateNode(_asset, typeof(JumpNode), new Vector2(0f, 600f));
            window.View.Rebuild();
            Assert.IsTrue(window.IssueList.HasPending, "validation waits for its delay");
            window.IssueList.ValidateNow();

            Assert.IsFalse(window.IssueList.HasPending);
            Assert.IsTrue(window.IssueList.Entries.Any(_entry => _entry.NodeId == _jump.Id && _entry.Text.Contains("Jump has no target")));
            Assert.IsTrue(window.View.FindNodeView(_jump.Id).HasWarningMark, "the node carries the mark");
            Assert.IsTrue(window.IssueCountLabel.text.Contains("warning"));
        }

        [Test]
        public void Window_IssueClick_SelectsNode() {
            DialogueAsset _asset = NewSample("IssueClick");
            window = DialogueGraphWindow.Open(_asset);
            JumpNode _jump = (JumpNode)DialogueGraphOps.CreateNode(_asset, typeof(JumpNode), new Vector2(0f, 600f));
            window.View.Rebuild();
            window.IssueList.ValidateNow();
            window.View.ClearSelection();

            int _index = -1;
            for (int _i = 0; _i < window.IssueList.Entries.Count; _i++) {
                if (window.IssueList.Entries[_i].NodeId == _jump.Id) _index = _i;
            }
            Assert.GreaterOrEqual(_index, 0);
            window.IssueList.Activate(_index);

            Assert.AreEqual(_jump.Id, window.View.SelectedNodeId);
        }

        [Test]
        public void Window_RevalidateAll_ListsEachDialogue_ClickOpensIt() {
            DialogueAsset _first = NewSample("AllFirst");
            DialogueAsset _second = NewSample("AllSecond");
            window = DialogueGraphWindow.Open(_first);

            window.IssueList.RevalidateAll();

            int _index = -1;
            for (int _i = 0; _i < window.IssueList.Entries.Count; _i++) {
                if (window.IssueList.Entries[_i].Asset == _second) _index = _i;
            }
            Assert.GreaterOrEqual(_index, 0, "a row per dialogue");
            window.IssueList.Activate(_index);
            Assert.AreSame(_second, window.Asset);
        }

        [Test]
        public void Window_UnrelatedChange_KeepsProjectSummary() {
            DialogueAsset _asset = NewSample("KeepSummary");
            window = DialogueGraphWindow.Open(_asset);
            window.IssueList.RevalidateAll();
            Assert.IsTrue(window.IssueList.Entries.Any(_entry => _entry.Asset != null));

            ScriptableObject _unrelated = ScriptableObject.CreateInstance<DialogueSettings>();
            try {
                Assert.IsFalse(window.NotifyObjectChanged(_unrelated));
            } finally {
                Object.DestroyImmediate(_unrelated);
            }
            Assert.IsFalse(window.IssueList.HasPending, "nothing waits to replace the summary");
            Assert.IsTrue(window.IssueList.Entries.Any(_entry => _entry.Asset != null), "the summary is still shown");

            Assert.IsTrue(window.NotifyObjectChanged(_asset), "a change of the shown dialogue counts");
            Assert.IsTrue(window.IssueList.HasPending);
        }

        [Test]
        public void Window_Reopen_DoesNotScanPrefabsAgain() {
            DialogueAsset _asset = NewSample("ScanOnce");
            window = DialogueGraphWindow.Open(_asset);
            int _scans = DialogueValidator.AmbientScanCount;

            // What a domain reload or entering Play does to the window
            window.OnDisable();
            window.OnEnable();
            window.IssueList.ValidateNow();
            Assert.AreEqual(_scans, DialogueValidator.AmbientScanCount, "the launchers found before are reused");

            window.IssueList.RevalidateAll();
            Assert.AreEqual(_scans + 1, DialogueValidator.AmbientScanCount, "Revalidate all looks again");
        }

        [Test]
        public void AmbientLaunches_KeptForTheSession() {
            string _saved = SessionState.GetString(DialogueIssueList.AmbientLaunchesKey, "");
            try {
                DialogueAsset _asset = NewAsset("KeptAmbient");
                DialogueIssueList.StoreAmbientLaunches(new HashSet<DialogueAsset> { _asset });
                CollectionAssert.AreEquivalent(new[] { _asset }, DialogueIssueList.LoadAmbientLaunches());

                DialogueIssueList.StoreAmbientLaunches(new HashSet<DialogueAsset>());
                HashSet<DialogueAsset> _none = DialogueIssueList.LoadAmbientLaunches();
                Assert.IsNotNull(_none, "a scan that found nothing is kept too");
                Assert.IsEmpty(_none);
            } finally {
                SessionState.SetString(DialogueIssueList.AmbientLaunchesKey, _saved);
            }
        }

        [Test]
        public void Window_JumpTooltip_KeptWithWarnings() {
            const string _doubleClick = "Double click to open the target dialogue";
            DialogueAsset _asset = NewSample("JumpTooltip");
            JumpNode _jump = (JumpNode)DialogueGraphOps.CreateNode(_asset, typeof(JumpNode), new Vector2(0f, 600f));
            window = DialogueGraphWindow.Open(_asset);
            window.IssueList.ValidateNow();
            DialogueNodeView _view = window.View.FindNodeView(_jump.Id);

            Assert.IsTrue(_view.HasWarningMark);
            StringAssert.Contains(_doubleClick, _view.tooltip);
            StringAssert.Contains("Jump has no target", _view.tooltip);

            _view.SetWarnings(null);
            Assert.AreEqual(_doubleClick, _view.tooltip);
        }

        [Test]
        public void Window_CopyPaste_SameDialogue_TextHasItsOwnEntry() {
            DialogueAsset _asset = NewSample("PasteSame");
            LineNode _line = FirstLine(_asset);
            Assert.IsTrue(DialogueTables.SetSourceText(_asset, _line.Text, _line.Id, "Original text"));
            window = DialogueGraphWindow.Open(_asset);
            window.View.UseTestClipboard();
            window.View.SelectAndFrame(_line.Id);

            window.View.CopySelection();
            Undo.IncrementCurrentGroup();
            window.View.Paste();

            LineNode _copy = _asset.Nodes.Skip(5).OfType<LineNode>().Single();
            Assert.AreNotEqual(_line.Id, _copy.Id);
            Assert.IsNotNull(DialogueTables.EnsureCollection(_asset).SharedData.GetEntry(_copy.Id), "entry keyed by the new id");
            Assert.AreEqual("Original text", DialogueTables.GetSourceText(_copy.Text));

            Assert.IsTrue(DialogueTables.SetSourceText(_asset, _copy.Text, _copy.Id, "Edited copy"));
            Assert.AreEqual("Edited copy", DialogueTables.GetSourceText(_copy.Text));
            Assert.AreEqual("Original text", DialogueTables.GetSourceText(_line.Text), "the original keeps its text");
        }

        [Test]
        public void Window_AssetRenamed_TitleFollows() {
            DialogueAsset _asset = NewSample("BeforeRename");
            window = DialogueGraphWindow.Open(_asset);
            Assert.AreEqual("BeforeRename", window.titleContent.text);

            Assert.IsEmpty(AssetDatabase.RenameAsset(AssetDatabase.GetAssetPath(_asset), "AfterRename"));
            window.OnProjectChange();

            Assert.AreEqual("AfterRename", window.titleContent.text);
        }

        private static PropertyField FindBoundField(VisualElement _root, string _name) {
            return _root.Query<PropertyField>().Where(_field => _field.bindingPath != null && (_field.bindingPath == _name || _field.bindingPath.EndsWith("." + _name))).First();
        }
        #endregion

        // ==================== NODE CONTENT ====================
        #region Node Content
        [Test]
        public void LinePreview_ShowsSourceTextWithoutEffectTags() {
            DialogueAsset _asset = NewSample("LinePreview");
            window = DialogueGraphWindow.Open(_asset);
            LineNode _line = FirstLine(_asset);
            LineNodeView _view = (LineNodeView)window.View.FindNodeView(_line.Id);
            Assert.AreEqual("", _view.Preview.FullText, "no text yet");

            // Written after the canvas was built: the preview follows the table
            DialogueTables.SetSourceText(_asset, _line.Text, _line.Id, "Hello<pause=0.5> there<event=ping>");

            Assert.AreEqual("Hello there", ((LineNodeView)window.View.FindNodeView(_line.Id)).Preview.FullText);
            Assert.IsEmpty(window.View.FindNodeView(_line.Id).Query<TextField>().ToList(), "no text field on the canvas");
        }

        [Test]
        public void OptionPreview_ShowsOptionText() {
            DialogueAsset _asset = NewSample("OptionPreview");
            ChoiceNode _choice = _asset.Nodes.OfType<ChoiceNode>().First();
            DialogueTables.SetSourceText(_asset, _choice.Options[1].Text, _choice.Options[1].Id, "Second<speed=2> answer\nnext row");
            window = DialogueGraphWindow.Open(_asset);
            ChoiceNodeView _view = (ChoiceNodeView)window.View.FindNodeView(_choice.Id);

            Assert.AreEqual("", _view.OptionPreview(0).FullText);
            Assert.AreEqual("Second answer next row", _view.OptionPreview(1).FullText, "one line, without effect tags");
            Assert.IsEmpty(_view.Query<TextField>().ToList(), "no text field on the canvas");
        }

        [UnityTest]
        public IEnumerator LinePreview_LongText_ClippedNodeHeightUnchanged() {
            DialogueAsset _asset = NewSample("LineClip");
            LineNode _short = FirstLine(_asset);
            LineNode _long = (LineNode)DialogueGraphOps.CreateNode(_asset, typeof(LineNode), new Vector2(200f, 300f));
            DialogueTables.SetSourceText(_asset, _short.Text, _short.Id, "Short");
            DialogueTables.SetSourceText(_asset, _long.Text, _long.Id, string.Join(" ", Enumerable.Repeat("A long sentence that keeps going", 12)));
            window = DialogueGraphWindow.Open(_asset);
            LineNodeView _shortView = (LineNodeView)window.View.FindNodeView(_short.Id);
            LineNodeView _longView = (LineNodeView)window.View.FindNodeView(_long.Id);

            for (int _i = 0; _i < MaxLayoutFrames && !(IsLaidOut(_shortView) && IsLaidOut(_longView) && _longView.Preview.text.EndsWith(Ellipsis)); _i++) yield return null;

            StringAssert.EndsWith(Ellipsis, _longView.Preview.text, "cut with an ellipsis");
            Assert.AreEqual("Short", _shortView.Preview.text);
            Assert.AreEqual(_shortView.layout.height, _longView.layout.height, 0.5f, "the node does not grow");
            Assert.AreEqual(_shortView.layout.width, _longView.layout.width, 0.5f, "same width");
            Vector2 _twoLines = _longView.Preview.MeasureTextSize("A\nA", 0f, VisualElement.MeasureMode.Undefined, 0f, VisualElement.MeasureMode.Undefined);
            Assert.LessOrEqual(_longView.Preview.MeasureTextSize(_longView.Preview.text, _longView.Preview.contentRect.width, VisualElement.MeasureMode.Exactly, 0f, VisualElement.MeasureMode.Undefined).y, _twoLines.y + 0.5f, "at most two lines");
        }

        [UnityTest]
        public IEnumerator ChoiceRows_EqualHeight_InsideNode() {
            DialogueAsset _asset = NewSample("ChoiceRows");
            ChoiceNode _choice = _asset.Nodes.OfType<ChoiceNode>().First();
            DialogueGraphOps.AddOption(_asset, _choice);
            DialogueTables.SetSourceText(_asset, _choice.Options[0].Text, _choice.Options[0].Id, "Yes");
            DialogueTables.SetSourceText(_asset, _choice.Options[2].Text, _choice.Options[2].Id, string.Join(" ", Enumerable.Repeat("A much longer answer", 10)));
            window = DialogueGraphWindow.Open(_asset);
            ChoiceNodeView _view = (ChoiceNodeView)window.View.FindNodeView(_choice.Id);
            LineNodeView _lineView = (LineNodeView)window.View.FindNodeView(FirstLine(_asset).Id);

            for (int _i = 0; _i < MaxLayoutFrames && !(IsLaidOut(_view) && IsLaidOut(_lineView) && _view.OptionPreview(2).text.EndsWith(Ellipsis)); _i++) yield return null;

            Assert.AreEqual(4, _view.Outputs.Count);
            StringAssert.EndsWith(Ellipsis, _view.OptionPreview(2).text);
            // In the node's own space, so the camera zoom does not matter; the border is the edge the user sees
            Rect _border = InNode(_view.Q("node-border"), _view);
            float _height = InNode(_view.Outputs[0].Port, _view).height;
            for (int _i = 0; _i < _view.Outputs.Count; _i++) {
                Rect _row = InNode(_view.Outputs[_i].Port, _view);
                Assert.AreEqual(_height, _row.height, 0.5f, "row " + _i + " height");
                Assert.GreaterOrEqual(_row.yMin, _border.yMin - 0.5f, "row " + _i + " top");
                Assert.LessOrEqual(_row.yMax, _border.yMax + 0.5f, "row " + _i + " bottom");
                Assert.GreaterOrEqual(_row.xMin, _border.xMin - 0.5f, "row " + _i + " left");
                Assert.LessOrEqual(_row.xMax, _border.xMax + 0.5f, "row " + _i + " right");
                Rect _connector = InNode(_view.Outputs[_i].Port.Q("connector"), _view);
                Assert.LessOrEqual(_border.xMax - _connector.xMax, MaxPortInset, "row " + _i + " connector against the right edge");
            }
            Rect _input = InNode(_view.Input.Q("connector"), _view);
            Assert.LessOrEqual(_input.xMin - _border.xMin, MaxPortInset, "input connector against the left edge");
            Assert.AreEqual(_lineView.layout.width, _view.layout.width, 0.5f, "content nodes share their width");
        }

        [Test]
        public void StartAndEnd_CompactWithTooltips() {
            DialogueAsset _asset = NewSample("Compact");
            window = DialogueGraphWindow.Open(_asset);
            DialogueNodeView _start = window.View.FindNodeView(_asset.Start.Id);
            DialogueNodeView _end = window.View.FindNodeView(_asset.Nodes.OfType<EndNode>().First().Id);

            Assert.IsTrue(_start.ClassListContains("dialogue-node--compact"));
            Assert.IsNull(_start.Input);
            Assert.AreEqual(1, _start.Outputs.Count);
            Assert.AreEqual("Entry point of the dialogue", _start.tooltip);

            Assert.IsTrue(_end.ClassListContains("dialogue-node--compact"));
            Assert.IsNotNull(_end.Input);
            Assert.AreEqual(0, _end.Outputs.Count);
            Assert.AreEqual("Ends the dialogue here. An unconnected output also ends it.", _end.tooltip);
        }

        [Test]
        public void Edge_PointingLeft_IsBackEdge() {
            DialogueAsset _asset = NewSample("BackEdge");
            LineNode _line = FirstLine(_asset);
            ChoiceNode _choice = _asset.Nodes.OfType<ChoiceNode>().First();
            window = DialogueGraphWindow.Open(_asset);

            window.View.Connect(_choice.Options[1].Next.Id, _line.Id);

            Assert.IsTrue(window.View.FindEdge(_choice.Options[1].Next.Id).IsBack, "choice to line points left");
            Assert.IsFalse(window.View.FindEdge(_line.Next.Id).IsBack, "line to choice points right");
            DialogueEdge _back = window.View.FindEdge(_choice.Options[1].Next.Id);
            Assert.Less(_back.edgeControl.inputColor.a, 0.5f, "drawn dimmer");
            Assert.AreEqual(1f, window.View.FindEdge(_line.Next.Id).edgeControl.inputColor.a, 0.01f);
            _back.Traversed = true;
            Assert.AreEqual(1f, _back.edgeControl.inputColor.a, 0.01f, "a traversed back edge is highlighted at full opacity");
            _back.Traversed = false;
            Assert.Less(_back.edgeControl.inputColor.a, 0.5f, "dim again once the run mark goes");

            window.View.Connect(_choice.NoAnswer.Id, _choice.Id);
            Assert.IsTrue(window.View.FindEdge(_choice.NoAnswer.Id).IsBack, "a self connection");

            // Moving the line right of the choice turns the edges around
            DialogueNodeView _lineView = window.View.FindNodeView(_line.Id);
            _lineView.SetPosition(new Rect(800f, 0f, 0f, 0f));
            window.View.graphViewChanged(new GraphViewChange { movedElements = new List<GraphElement> { _lineView } });

            Assert.IsTrue(window.View.FindEdge(_line.Next.Id).IsBack, "line to choice after the move");
            Assert.IsFalse(window.View.FindEdge(_choice.Options[1].Next.Id).IsBack, "choice to line after the move");
        }

        private static bool IsLaidOut(VisualElement _element) => !float.IsNaN(_element.layout.width) && _element.layout.width > 0f;

        private static Rect InNode(VisualElement _element, VisualElement _node) => _element.ChangeCoordinatesTo(_node, new Rect(Vector2.zero, _element.layout.size));
        #endregion

        // ==================== PANEL ====================
        #region Panel
        [Test]
        public void Panel_Sections_InOrderPerNodeType() {
            DialogueAsset _asset = NewSample("Sections");
            window = DialogueGraphWindow.Open(_asset);

            window.View.SelectAndFrame(FirstLine(_asset).Id);
            CollectionAssert.AreEqual(new[] { "Text", "Translations", "Timing", "Actions", "Text Events" }, window.Panel.SectionTitles);

            window.View.SelectAndFrame(_asset.Nodes.OfType<ChoiceNode>().First().Id);
            CollectionAssert.AreEqual(new[] { "Timer", "Options" }, window.Panel.SectionTitles);
            Assert.AreEqual(2, window.Panel.ItemCount);

            window.View.SelectAndFrame(_asset.Nodes.OfType<BranchNode>().First().Id);
            Assert.AreEqual(1, window.Panel.ItemCount);

            window.View.SelectAndFrame(_asset.Start.Id);
            CollectionAssert.IsEmpty(window.Panel.SectionTitles);
            Assert.IsNull(FindBoundField(window.Panel, "next"), "outputs are not shown");
        }

        [Test]
        public void Panel_SectionState_RememberedPerNodeType() {
            DialogueAsset _asset = NewSample("SectionState");
            window = DialogueGraphWindow.Open(_asset);
            LineNode _line = FirstLine(_asset);
            string _key = DialogueInspectorPanel.SectionStateKey(typeof(LineNode), "Timing");
            bool _hadKey = SessionState.GetInt(_key, -1) != -1;
            bool _had = SessionState.GetBool(_key, false);
            try {
                window.View.SelectAndFrame(_line.Id);
                Foldout _timing = window.Panel.Section("Timing");
                _timing.value = !_timing.value;
                bool _set = _timing.value;

                window.View.SelectAndFrame(_asset.Nodes.OfType<ChoiceNode>().First().Id);
                window.View.SelectAndFrame(_line.Id);

                Assert.AreEqual(_set, window.Panel.Section("Timing").value);
            } finally {
                if (_hadKey) SessionState.SetBool(_key, _had);
                else SessionState.EraseBool(_key);
            }
        }

        [Test]
        public void Panel_Translation_WritesLocaleTable() {
            DialogueAsset _asset = NewSample("PanelTranslation");
            LineNode _line = FirstLine(_asset);
            DialogueTables.SetSourceText(_asset, _line.Text, _line.Id, "Hello");
            window = DialogueGraphWindow.Open(_asset);
            window.View.SelectAndFrame(_line.Id);

            TextField _field = window.Panel.TranslationField(_line.Id, OtherCode);
            Assert.IsNotNull(_field);
            Assert.IsNull(window.Panel.TranslationField(_line.Id, SourceCode), "the source locale is edited above");
            Assert.AreEqual("", _field.value);
            Assert.AreEqual("Not translated", _field.textEdition.placeholder);

            Undo.IncrementCurrentGroup();
            _field.value = "Hola";

            Assert.AreEqual("Hola", DialogueTables.GetText(_line.Text.TableReference, _line.Text.TableEntryReference, new LocaleIdentifier(OtherCode)));
            Assert.AreEqual("Hello", DialogueTables.GetSourceText(_line.Text));
            Undo.PerformUndo();
            Assert.IsTrue(string.IsNullOrEmpty(DialogueTables.GetText(_line.Text.TableReference, _line.Text.TableEntryReference, new LocaleIdentifier(OtherCode))), "one undo takes it back");
        }

        [Test]
        public void Panel_TranslationField_ReadsExternalEditOnFocus() {
            DialogueAsset _asset = NewSample("PanelExternalEdit");
            LineNode _line = FirstLine(_asset);
            DialogueTables.SetSourceText(_asset, _line.Text, _line.Id, "Hello");
            window = DialogueGraphWindow.Open(_asset);
            window.View.SelectAndFrame(_line.Id);
            TextField _field = window.Panel.TranslationField(_line.Id, OtherCode);
            Assert.AreEqual("", _field.value);

            // Written outside the panel, as the Localization Tables window does
            StringTableCollection _collection = DialogueTables.EnsureCollection(_asset);
            StringTable _table = (StringTable)_collection.GetTable(new LocaleIdentifier(OtherCode));
            _table.AddEntry(_collection.SharedData.GetEntry(_line.Id).Id, "Hola desde fuera");
            using (FocusInEvent _event = FocusInEvent.GetPooled(_field, null, FocusChangeDirection.unspecified, _field.focusController)) {
                _field.SendEvent(_event);
            }

            Assert.AreEqual("Hola desde fuera", _field.value, "focusing the field reads the table again");
        }

        [Test]
        public void Panel_OptionTranslations_ForTextAndLockedReason() {
            DialogueAsset _asset = NewSample("PanelOptionTranslation");
            ChoiceNode _choice = _asset.Nodes.OfType<ChoiceNode>().First();
            ChoiceOption _option = _choice.Options[0];
            window = DialogueGraphWindow.Open(_asset);
            window.View.SelectAndFrame(_choice.Id);

            Assert.IsNotNull(window.Panel.TranslationField(_option.Id, OtherCode), "option text");
            window.Panel.TranslationField(_option.Id + ChoiceOption.LockedSuffix, OtherCode).value = "Bloqueada";

            Assert.AreEqual("Bloqueada", DialogueTables.GetText(_option.LockedReason.TableReference, _option.LockedReason.TableEntryReference, new LocaleIdentifier(OtherCode)));
        }
        #endregion

        // ==================== PLAY MODE ====================
        #region Play Mode
        /// <summary> Runs dialogues by hand, their lines held until advanced; undone in <see cref="TearDown"/> </summary>
        private FakeLineChannel StartRunning() {
            runsStarted = true;
            // Runs left by a Play session would keep the main slot taken
            Dialogue.ClearForTests();
            DialogueMemory.ClearForTests();
            Dialogue.ManualTick = true;
            FakeLineChannel _channel = new FakeLineChannel();
            Dialogue.ChannelResolver = _ => _channel;
            return _channel;
        }

        private static void TickRuns() => Dialogue.TickAll(0f, 0f);

        private static LineNode FirstLine(DialogueAsset _asset) => _asset.Nodes.OfType<LineNode>().First();

        [Test]
        public void OpenDuringRun_HighlightsCurrent() {
            DialogueAsset _asset = NewSample("OpenDuringRun");
            FakeLineChannel _channel = StartRunning();
            LineNode _line = FirstLine(_asset);
            ChoiceNode _choice = _asset.Nodes.OfType<ChoiceNode>().First();
            Dialogue.Start(_asset);
            TickRuns();

            window = DialogueGraphWindow.Open(_asset);
            window.BeginPlayMode();

            Assert.IsTrue(window.View.FindNodeView(_line.Id).ClassListContains("current"), "the node the run is on lights up at once");
            Assert.IsFalse(window.View.FindNodeView(_choice.Id).ClassListContains("current"));

            _channel.Last.Advance();
            TickRuns();
            Assert.IsFalse(window.View.FindNodeView(_line.Id).ClassListContains("current"));
            Assert.IsTrue(window.View.FindNodeView(_choice.Id).ClassListContains("current"));
            Assert.IsTrue(window.View.FindEdge(_line.Next.Id).ClassListContains("traversed"), "the output the run took");
            Assert.IsFalse(window.View.FindEdge(_choice.Options[0].Next.Id).ClassListContains("traversed"));
        }

        [Test]
        public void Window_ManualPan_DisablesFollow() {
            DialogueAsset _asset = NewSample("ManualPan");
            StartRunning();
            window = DialogueGraphWindow.Open(_asset);
            window.BeginPlayMode();
            Assert.IsTrue(window.Follow, "follow is on when play starts");

            Dialogue.Start(_asset);
            TickRuns();
            Assert.IsTrue(window.Follow, "the window framing the node itself keeps follow on");

            // What the content dragger and zoomer do
            window.View.UpdateViewTransform(new Vector3(123f, 45f, 0f), Vector3.one);
            Assert.IsFalse(window.Follow);
        }

        [Test]
        public void Window_Follow_ShowsTheRunDialogue() {
            DialogueAsset _shown = NewSample("FollowShown");
            DialogueAsset _other = NewSample("FollowOther");
            StartRunning();
            window = DialogueGraphWindow.Open(_shown);
            window.BeginPlayMode();
            window.Follow = false;

            Dialogue.Start(_other);
            TickRuns();
            Assert.AreSame(_shown, window.Asset, "without follow the open dialogue stays");

            window.Follow = true;
            Assert.AreSame(_other, window.Asset, "with follow the window shows the followed run's dialogue");
            Assert.IsTrue(window.View.FindNodeView(FirstLine(_other).Id).ClassListContains("current"));
        }

        [Test]
        public void Window_PlayMode_IsReadOnly() {
            DialogueAsset _asset = NewSample("ReadOnly");
            window = DialogueGraphWindow.Open(_asset);
            LineNode _line = FirstLine(_asset);

            window.BeginPlayMode();
            LineNodeView _view = (LineNodeView)window.View.FindNodeView(_line.Id);
            Assert.AreEqual((Capabilities)0, _view.capabilities & (Capabilities.Movable | Capabilities.Deletable));
            Assert.IsTrue(_view.Outputs.All(_output => _output.Port.edgeConnector == null), "no new connections");
            window.View.SelectAndFrame(_line.Id);
            Assert.IsTrue(window.Panel.Query<PropertyField>().ToList().All(_field => !_field.enabledSelf), "panel fields");
            TextField _translation = window.Panel.TranslationField(_line.Id, OtherCode);
            Assert.IsNotNull(_translation, "translation field");
            Assert.IsFalse(_translation.enabledSelf, "translation fields");
            int _count = _asset.Nodes.Count;
            window.View.CreateNode(typeof(EndNode), Vector2.zero, null);
            Assert.AreEqual(_count, _asset.Nodes.Count, "no nodes created");

            window.EndPlayMode();
            _view = (LineNodeView)window.View.FindNodeView(_line.Id);
            Assert.IsTrue(window.Panel.TranslationField(_line.Id, OtherCode)?.enabledSelf ?? false, "translation fields editable again");
            Assert.AreNotEqual((Capabilities)0, _view.capabilities & Capabilities.Movable);
            Assert.IsTrue(window.Panel.Query<PropertyField>().ToList().Any(_field => _field.enabledSelf));
        }

        [Test]
        public void Window_PlayMode_EditsLeaveAssetUnchanged() {
            DialogueAsset _asset = NewSample("ReadOnlyEdits");
            LineNode _line = FirstLine(_asset);
            ChoiceNode _choice = _asset.Nodes.OfType<ChoiceNode>().First();
            DialogueGroup _group = DialogueGraphOps.CreateGroup(_asset, "Group", new List<string> { _line.Id });
            window = DialogueGraphWindow.Open(_asset);
            window.View.SelectAndFrame(_line.Id);
            window.View.CopySelection();

            window.BeginPlayMode();
            string _before = EditorJsonUtility.ToJson(_asset);

            window.View.SelectAndFrame(_line.Id);
            window.View.DuplicateSelection();
            Assert.AreEqual(_before, EditorJsonUtility.ToJson(_asset), "duplicate");
            window.View.Paste();
            Assert.AreEqual(_before, EditorJsonUtility.ToJson(_asset), "paste");
            window.View.SelectAndFrame(_line.Id);
            window.View.DeleteSelection();
            Assert.AreEqual(_before, EditorJsonUtility.ToJson(_asset), "delete");
            window.View.SelectAndFrame(_line.Id);
            window.View.GroupSelection();
            Assert.AreEqual(_before, EditorJsonUtility.ToJson(_asset), "group");
            DialogueGroupView _groupView = window.View.FindGroupView(_group.Id);
            _groupView.title = "Renamed";
            Assert.AreEqual(_before, EditorJsonUtility.ToJson(_asset), "group rename");
            Assert.AreEqual("Group", _groupView.title, "the stored title is shown again");
            window.View.SelectAndFrame(_choice.Id);
            window.Panel.AddItem();
            Assert.AreEqual(_before, EditorJsonUtility.ToJson(_asset), "panel add option");
            Assert.IsNotNull(window.View.FindNodeView(_line.Id), "the canvas still shows the node");
        }

        [Test]
        public void Window_PlayMode_LoadDoesNotLayOut() {
            DialogueAsset _asset = NewAsset("ReadOnlyAtOrigin");
            DialogueGraphOps.CreateConnected(_asset, typeof(LineNode), Vector2.zero, _asset.Start.Next.Id);
            Undo.ClearAll();
            Assert.IsTrue(DialogueGraphOps.NeedsAutoLayout(_asset));
            window = DialogueGraphWindow.Open(null);
            window.BeginPlayMode();
            string _before = EditorJsonUtility.ToJson(_asset);

            window.SetAsset(_asset);

            Assert.AreEqual(_before, EditorJsonUtility.ToJson(_asset));
        }

        [Test]
        public void Window_RunMenu_MainFirstThenEndedFollowed() {
            DialogueAsset _asset = NewSample("RunMenu");
            StartRunning();
            DialogueRun _ended = Dialogue.Start(_asset);
            _ended.Stop();
            DialogueRun _ambient = Dialogue.Start(_asset, new DialogueStartOptions { IsMain = false });
            DialogueRun _main = Dialogue.Start(_asset);

            List<DialogueRun> _runs = DialogueGraphWindow.MenuRuns(_ended, Dialogue.Active);

            CollectionAssert.AreEqual(new[] { _main, _ended, _ambient }, _runs);
        }

        [Test]
        public void Window_SourceTextWritten_SchedulesValidation() {
            DialogueAsset _asset = NewSample("TextWritten");
            window = DialogueGraphWindow.Open(_asset);
            window.IssueList.ValidateNow();
            Assert.IsFalse(window.IssueList.HasPending);

            DialogueTables.NotifySourceTextWritten(_asset);

            Assert.IsTrue(window.IssueList.HasPending);
        }
        #endregion
    }
}
