using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarryTools.Dialogue.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HarryTools.Dialogue.Tests {
    public class DialogueGraphWindowTests {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string TempFolder = "Assets/__DialogueTests__";
        private const string AssetsFolder = TempFolder + "/GraphWindow";
        private const string TablesFolder = TempFolder + "/GraphWindowTables";

        private DialogueSettings settings;
        private DialogueGraphWindow window;
        #endregion

        // ==================== SETUP ====================
        #region Setup
        [SetUp]
        public void SetUp() {
            settings = ScriptableObject.CreateInstance<DialogueSettings>();
            settings.TablesFolder = TablesFolder;
            DialogueSettings.UseForTests(settings);
            EnsureFolder(TempFolder);
            EnsureFolder(AssetsFolder);
            Undo.IncrementCurrentGroup();
        }

        [TearDown]
        public void TearDown() {
            if (window != null) window.Close();
            window = null;

            DialogueSettings.ClearForTests();
            Object.DestroyImmediate(settings);

            if (AssetDatabase.IsValidFolder(TablesFolder)) AssetDatabase.DeleteAsset(TablesFolder);
            if (AssetDatabase.IsValidFolder(AssetsFolder)) AssetDatabase.DeleteAsset(AssetsFolder);
            if (AssetDatabase.IsValidFolder(TempFolder) && AssetDatabase.FindAssets("", new[] { TempFolder }).Length == 0) AssetDatabase.DeleteAsset(TempFolder);
            Undo.ClearAll();
        }

        private static void EnsureFolder(string _path) {
            if (AssetDatabase.IsValidFolder(_path)) return;
            int _slash = _path.LastIndexOf('/');
            AssetDatabase.CreateFolder(_path.Substring(0, _slash), _path.Substring(_slash + 1));
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
        #endregion
    }
}
