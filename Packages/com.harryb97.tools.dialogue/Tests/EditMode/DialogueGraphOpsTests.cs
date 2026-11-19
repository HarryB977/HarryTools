using System;
using System.Collections.Generic;
using HarryTools.Dialogue.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using Object = UnityEngine.Object;

namespace HarryTools.Dialogue.Tests {
    public class DialogueGraphOpsTests {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string TempFolder = "Assets/__DialogueTests__";
        private const string AssetsFolder = TempFolder + "/GraphOps";
        private const string TablesFolder = TempFolder + "/GraphOpsTables";
        private const string SourceCode = "en";
        private const string OtherCode = "es";

        private DialogueSettings settings;
        #endregion

        // ==================== SETUP ====================
        #region Setup
        [OneTimeSetUp]
        public void OneTimeSetUp() {
            EnsureFolder(TempFolder);
            TestLocales.RequireOrIgnore(SourceCode, OtherCode);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown() {
            if (AssetDatabase.IsValidFolder(TempFolder)) AssetDatabase.DeleteAsset(TempFolder);
        }

        [SetUp]
        public void SetUp() {
            settings = ScriptableObject.CreateInstance<DialogueSettings>();
            settings.TablesFolder = TablesFolder;
            settings.SourceLocaleCode = SourceCode;
            DialogueSettings.UseForTests(settings);
            EnsureFolder(AssetsFolder);
            Undo.IncrementCurrentGroup();
        }

        [TearDown]
        public void TearDown() {
            DialogueSettings.ClearForTests();
            Object.DestroyImmediate(settings);

            DeleteContents(TablesFolder);
            if (AssetDatabase.IsValidFolder(TablesFolder)) AssetDatabase.DeleteAsset(TablesFolder);
            if (AssetDatabase.IsValidFolder(AssetsFolder)) AssetDatabase.DeleteAsset(AssetsFolder);
            Undo.ClearAll();
        }

        private static void EnsureFolder(string _path) {
            if (AssetDatabase.IsValidFolder(_path)) return;
            int _slash = _path.LastIndexOf('/');
            AssetDatabase.CreateFolder(_path.Substring(0, _slash), _path.Substring(_slash + 1));
        }

        private static void DeleteContents(string _folder) {
            if (!AssetDatabase.IsValidFolder(_folder)) return;
            foreach (string _guid in AssetDatabase.FindAssets("t:StringTableCollection", new[] { _folder })) {
                AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(_guid));
            }
            foreach (string _guid in AssetDatabase.FindAssets("", new[] { _folder })) {
                string _path = AssetDatabase.GUIDToAssetPath(_guid);
                if (!string.IsNullOrEmpty(_path)) AssetDatabase.DeleteAsset(_path);
            }
        }

        private static DialogueAsset NewAsset(string _name) {
            DialogueAsset _asset = ScriptableObject.CreateInstance<DialogueAsset>();
            // OnValidate fills the ids when the asset is edited in the inspector
            _asset.EnsureIds();
            AssetDatabase.CreateAsset(_asset, AssetsFolder + "/" + _name + ".asset");
            return _asset;
        }

        private static void UndoLast() {
            Undo.PerformUndo();
        }
        #endregion

        // ==================== TESTS ====================
        #region Nodes
        [Test]
        public void CreateNode_AddsWithPosition_UndoRemoves() {
            DialogueAsset _asset = NewAsset("Create");

            DialogueNode _node = DialogueGraphOps.CreateNode(_asset, typeof(LineNode), new Vector2(40f, 60f));

            Assert.IsInstanceOf<LineNode>(_node);
            Assert.AreEqual(new Vector2(40f, 60f), _node.Position);
            Assert.IsFalse(string.IsNullOrEmpty(_node.Id));
            Assert.AreSame(_node, _asset.Find(_node.Id));
            Assert.IsTrue(EditorUtility.IsDirty(_asset));
            string _id = _node.Id;

            UndoLast();

            Assert.AreEqual(1, _asset.Nodes.Count);
            Assert.IsNull(_asset.Find(_id));
        }

        [Test]
        public void CreateNode_Start_ReturnsNull() {
            DialogueAsset _asset = NewAsset("CreateStart");

            Assert.IsNull(DialogueGraphOps.CreateNode(_asset, typeof(StartNode), Vector2.zero));
            Assert.IsNull(DialogueGraphOps.CreateNode(_asset, typeof(string), Vector2.zero));
            Assert.AreEqual(1, _asset.Nodes.Count);
        }

        [Test]
        public void CreateConnected_ConnectsOutput() {
            DialogueAsset _asset = NewAsset("CreateConnected");

            DialogueNode _node = DialogueGraphOps.CreateConnected(_asset, typeof(LineNode), new Vector2(10f, 0f), _asset.Start.Next.Id);

            Assert.AreEqual(_node.Id, _asset.Start.Next.Target);
            Assert.AreSame(_asset.Start, DialogueGraphOps.OwnerOf(_asset, _asset.Start.Next.Id));
            Assert.AreSame(_asset.Start.Next, DialogueGraphOps.FindOutput(_asset, _asset.Start.Next.Id));
        }

        [Test]
        public void Connect_ReplacesPreviousTarget() {
            DialogueAsset _asset = NewAsset("Connect");
            LineNode _a = (LineNode)DialogueGraphOps.CreateNode(_asset, typeof(LineNode), Vector2.zero);
            LineNode _b = (LineNode)DialogueGraphOps.CreateNode(_asset, typeof(LineNode), Vector2.zero);
            string _output = _asset.Start.Next.Id;

            DialogueGraphOps.Connect(_asset, _output, _a.Id);
            DialogueGraphOps.Connect(_asset, _output, _b.Id);
            Assert.AreEqual(_b.Id, _asset.Start.Next.Target);

            DialogueGraphOps.Disconnect(_asset, _output);
            Assert.IsFalse(_asset.Start.Next.IsConnected);
        }

        [Test]
        public void Delete_ClearsIncomingOutputs_KeepsStart() {
            DialogueAsset _asset = NewAsset("Delete");
            LineNode _a = (LineNode)DialogueGraphOps.CreateConnected(_asset, typeof(LineNode), Vector2.zero, _asset.Start.Next.Id);
            string _id = _a.Id;

            Undo.IncrementCurrentGroup();
            DialogueGraphOps.Delete(_asset, new[] { _id, _asset.Start.Id });

            Assert.AreEqual(1, _asset.Nodes.Count);
            Assert.IsNotNull(_asset.Start);
            Assert.IsFalse(_asset.Start.Next.IsConnected);

            UndoLast();
            Assert.AreEqual(2, _asset.Nodes.Count);
            Assert.AreEqual(_id, _asset.Start.Next.Target);
        }

        [Test]
        public void Delete_GroupAndNote_ById() {
            DialogueAsset _asset = NewAsset("DeleteBoxes");
            LineNode _a = (LineNode)DialogueGraphOps.CreateNode(_asset, typeof(LineNode), Vector2.zero);
            DialogueGroup _group = DialogueGraphOps.CreateGroup(_asset, "G", new[] { _a.Id });
            DialogueNote _note = DialogueGraphOps.CreateNote(_asset, new Rect(1f, 2f, 3f, 4f));

            DialogueGraphOps.Delete(_asset, new[] { _group.Id, _note.Id });

            Assert.AreEqual(0, _asset.Groups.Count);
            Assert.AreEqual(0, _asset.Notes.Count);
            Assert.AreSame(_a, _asset.Find(_a.Id));
        }

        [Test]
        public void Move_NodesAndNotes() {
            DialogueAsset _asset = NewAsset("Move");
            LineNode _a = (LineNode)DialogueGraphOps.CreateNode(_asset, typeof(LineNode), Vector2.zero);
            DialogueNote _note = DialogueGraphOps.CreateNote(_asset, new Rect(1f, 2f, 30f, 40f));

            DialogueGraphOps.Move(_asset, new Dictionary<string, Vector2> {
                { _a.Id, new Vector2(5f, 6f) },
                { _note.Id, new Vector2(7f, 8f) }
            });

            Assert.AreEqual(new Vector2(5f, 6f), _a.Position);
            Assert.AreEqual(new Rect(7f, 8f, 30f, 40f), _asset.Notes[0].Rect);
        }
        #endregion

        #region Options and Cases
        [Test]
        public void AddOption_NewIds_RemoveOption_Undo() {
            DialogueAsset _asset = NewAsset("Options");
            ChoiceNode _choice = (ChoiceNode)DialogueGraphOps.CreateNode(_asset, typeof(ChoiceNode), Vector2.zero);

            ChoiceOption _first = DialogueGraphOps.AddOption(_asset, _choice);
            ChoiceOption _second = DialogueGraphOps.AddOption(_asset, _choice);

            Assert.AreEqual(2, _choice.Options.Count);
            Assert.IsFalse(string.IsNullOrEmpty(_first.Id));
            Assert.IsFalse(string.IsNullOrEmpty(_first.Next.Id));
            Assert.AreNotEqual(_first.Id, _second.Id);
            Assert.AreNotEqual(_first.Next.Id, _second.Next.Id);
            string _firstId = _first.Id;
            string _secondId = _second.Id;

            Undo.IncrementCurrentGroup();
            DialogueGraphOps.RemoveOption(_asset, _choice, _firstId);
            Assert.AreEqual(1, _choice.Options.Count);
            Assert.AreEqual(_secondId, _choice.Options[0].Id);

            UndoLast();
            Assert.AreEqual(2, _choice.Options.Count);
            Assert.AreEqual(_firstId, _choice.Options[0].Id);
        }

        [Test]
        public void MoveOption_KeepsConnections() {
            DialogueAsset _asset = NewAsset("MoveOption");
            ChoiceNode _choice = (ChoiceNode)DialogueGraphOps.CreateNode(_asset, typeof(ChoiceNode), Vector2.zero);
            ChoiceOption _first = DialogueGraphOps.AddOption(_asset, _choice);
            ChoiceOption _second = DialogueGraphOps.AddOption(_asset, _choice);
            LineNode _line = (LineNode)DialogueGraphOps.CreateConnected(_asset, typeof(LineNode), Vector2.zero, _first.Next.Id);

            DialogueGraphOps.MoveOption(_asset, _choice, 0, 1);

            Assert.AreEqual(_second.Id, _choice.Options[0].Id);
            Assert.AreEqual(_first.Id, _choice.Options[1].Id);
            Assert.AreEqual(_line.Id, _choice.Options[1].Next.Target);
        }

        [Test]
        public void AddCase_RemoveCase_MoveCase() {
            DialogueAsset _asset = NewAsset("Cases");
            BranchNode _branch = (BranchNode)DialogueGraphOps.CreateNode(_asset, typeof(BranchNode), Vector2.zero);

            BranchCase _a = DialogueGraphOps.AddCase(_asset, _branch);
            BranchCase _b = DialogueGraphOps.AddCase(_asset, _branch);
            Assert.IsFalse(string.IsNullOrEmpty(_a.Next.Id));
            Assert.AreNotEqual(_a.Next.Id, _b.Next.Id);

            DialogueGraphOps.MoveCase(_asset, _branch, 0, 1);
            Assert.AreSame(_b, _branch.Cases[0]);
            Assert.AreSame(_a, _branch.Cases[1]);

            DialogueGraphOps.RemoveCase(_asset, _branch, 0);
            Assert.AreEqual(1, _branch.Cases.Count);
            Assert.AreSame(_a, _branch.Cases[0]);
        }
        #endregion

        #region Groups and Notes
        [Test]
        public void Group_NodeInOneGroupOnly() {
            DialogueAsset _asset = NewAsset("Groups");
            LineNode _a = (LineNode)DialogueGraphOps.CreateNode(_asset, typeof(LineNode), Vector2.zero);
            LineNode _b = (LineNode)DialogueGraphOps.CreateNode(_asset, typeof(LineNode), Vector2.zero);

            DialogueGroup _first = DialogueGraphOps.CreateGroup(_asset, "One", new[] { _a.Id, _b.Id });
            DialogueGroup _second = DialogueGraphOps.CreateGroup(_asset, "Two", new[] { _b.Id });

            CollectionAssert.AreEqual(new[] { _a.Id }, _first.NodeIds);
            CollectionAssert.AreEqual(new[] { _b.Id }, _second.NodeIds);

            DialogueGraphOps.SetGroupNodes(_asset, _first.Id, new[] { _a.Id, _b.Id });
            Assert.AreEqual(2, _first.NodeIds.Count);
            Assert.AreEqual(0, _second.NodeIds.Count);
            CollectionAssert.AreEqual(new[] { _first }, _asset.Groups, "a group left without nodes is removed");

            DialogueGraphOps.SetGroup(_asset, _first.Id, "Renamed", Color.red);
            Assert.AreEqual("Renamed", _first.Title);
            Assert.AreEqual(Color.red, _first.Color);
        }

        [Test]
        public void Note_CreateAndSet() {
            DialogueAsset _asset = NewAsset("Notes");

            DialogueNote _note = DialogueGraphOps.CreateNote(_asset, new Rect(0f, 0f, 50f, 60f));
            DialogueGraphOps.SetNote(_asset, _note.Id, "hello", new Rect(1f, 2f, 70f, 80f));

            Assert.AreEqual("hello", _note.Text);
            Assert.AreEqual(new Rect(1f, 2f, 70f, 80f), _note.Rect);
        }
        #endregion

        #region Clipboard
        [Test]
        public void Paste_SameDialogue_NewIds_InternalLinksKept_ExternalCleared() {
            DialogueAsset _asset = NewAsset("PasteSame");
            LineNode _a = (LineNode)DialogueGraphOps.CreateConnected(_asset, typeof(LineNode), new Vector2(100f, 100f), _asset.Start.Next.Id);
            LineNode _b = (LineNode)DialogueGraphOps.CreateConnected(_asset, typeof(LineNode), new Vector2(200f, 100f), _a.Next.Id);
            LineNode _c = (LineNode)DialogueGraphOps.CreateConnected(_asset, typeof(LineNode), new Vector2(300f, 100f), _b.Next.Id);
            DialogueGroup _group = DialogueGraphOps.CreateGroup(_asset, "G", new[] { _a.Id, _b.Id });
            DialogueNote _note = DialogueGraphOps.CreateNote(_asset, new Rect(10f, 10f, 20f, 20f));

            string _data = DialogueClipboard.Copy(_asset, new[] { _a.Id, _b.Id, _asset.Start.Id, _group.Id, _note.Id });
            Assert.IsTrue(DialogueClipboard.CanPaste(_data));
            Undo.IncrementCurrentGroup();
            IReadOnlyList<string> _ids = DialogueClipboard.Paste(_asset, _data, new Vector2(0f, 50f));

            // 2 nodes, 1 group, 1 note
            Assert.AreEqual(4, _ids.Count);
            Assert.AreEqual(6, _asset.Nodes.Count);
            LineNode _newA = (LineNode)_asset.Find(_ids[0]);
            LineNode _newB = (LineNode)_asset.Find(_ids[1]);
            Assert.IsNotNull(_newA);
            Assert.IsNotNull(_newB);
            Assert.AreNotEqual(_a.Id, _newA.Id);
            Assert.AreEqual(new Vector2(100f, 150f), _newA.Position);
            Assert.AreEqual(_newB.Id, _newA.Next.Target);
            Assert.AreEqual("", _newB.Next.Target);
            Assert.AreNotEqual(_a.Next.Id, _newA.Next.Id);
            Assert.AreEqual(_b.Id, _a.Next.Target);
            Assert.AreEqual(_c.Id, _b.Next.Target);

            Assert.AreEqual(2, _asset.Groups.Count);
            DialogueGroup _newGroup = _asset.Groups[1];
            Assert.AreEqual(_ids[2], _newGroup.Id);
            Assert.AreNotEqual(_group.Id, _newGroup.Id);
            CollectionAssert.AreEquivalent(new[] { _newA.Id, _newB.Id }, _newGroup.NodeIds);
            CollectionAssert.AreEquivalent(new[] { _a.Id, _b.Id }, _group.NodeIds);
            Assert.AreEqual(2, _asset.Notes.Count);
            Assert.AreEqual(new Rect(10f, 60f, 20f, 20f), _asset.Notes[1].Rect);
            Assert.AreEqual(_ids[3], _asset.Notes[1].Id);

            UndoLast();
            Assert.AreEqual(4, _asset.Nodes.Count);
            Assert.AreEqual(1, _asset.Groups.Count);
            Assert.AreEqual(1, _asset.Notes.Count);
        }

        [Test]
        public void Paste_IntoOtherDialogue_OwnTextsNoDanglingTargets() {
            DialogueAsset _source = NewAsset("PasteSource");
            LineNode _a = (LineNode)DialogueGraphOps.CreateConnected(_source, typeof(LineNode), Vector2.zero, _source.Start.Next.Id);
            LineNode _b = (LineNode)DialogueGraphOps.CreateConnected(_source, typeof(LineNode), Vector2.zero, _a.Next.Id);
            DialogueGraphOps.CreateConnected(_source, typeof(LineNode), Vector2.zero, _b.Next.Id);
            DialogueTables.SetSourceText(_source, _a.Text, _a.Id, "Hello");
            DialogueAsset _target = NewAsset("PasteTarget");

            // The last node is not copied, so the pasted line must not point at it
            string _data = DialogueClipboard.Copy(_source, new[] { _a.Id, _b.Id });
            IReadOnlyList<string> _ids = DialogueClipboard.Paste(_target, _data, Vector2.zero);

            Assert.AreEqual(2, _ids.Count);
            LineNode _newA = (LineNode)_target.Find(_ids[0]);
            LineNode _newB = (LineNode)_target.Find(_ids[1]);
            Assert.AreNotEqual(_a.Id, _newA.Id);
            Assert.AreEqual(_newB.Id, _newA.Next.Target);
            Assert.AreEqual("", _newB.Next.Target);
            Assert.AreEqual(3, _target.Nodes.Count);

            StringTableCollection _own = LocalizationEditorSettings.GetStringTableCollection(Guid.Parse(_target.TableCollectionGuid));
            Assert.IsNotNull(_own);
            Assert.AreEqual(_own.SharedData.TableCollectionNameGuid, _newA.Text.TableReference.TableCollectionNameGuid);
            Assert.AreEqual(_newA.Id, _own.SharedData.GetEntryFromReference(_newA.Text.TableEntryReference).Key);
            Assert.AreEqual("Hello", DialogueTables.GetSourceText(_newA.Text));
            Assert.AreEqual("Hello", DialogueTables.GetSourceText(_a.Text));
        }

        [Test]
        public void Paste_Choice_OptionTextsFollowNewOptionIds() {
            DialogueAsset _source = NewAsset("PasteChoiceSource");
            ChoiceNode _choice = (ChoiceNode)DialogueGraphOps.CreateNode(_source, typeof(ChoiceNode), Vector2.zero);
            ChoiceOption _option = DialogueGraphOps.AddOption(_source, _choice);
            DialogueTables.SetSourceText(_source, _option.Text, _option.Id, "Yes");
            DialogueAsset _target = NewAsset("PasteChoiceTarget");

            IReadOnlyList<string> _ids = DialogueClipboard.Paste(_target, DialogueClipboard.Copy(_source, new[] { _choice.Id }), Vector2.zero);

            ChoiceNode _new = (ChoiceNode)_target.Find(_ids[0]);
            ChoiceOption _newOption = _new.Options[0];
            Assert.AreNotEqual(_option.Id, _newOption.Id);
            StringTableCollection _own = LocalizationEditorSettings.GetStringTableCollection(Guid.Parse(_target.TableCollectionGuid));
            Assert.AreEqual(_newOption.Id, _own.SharedData.GetEntryFromReference(_newOption.Text.TableEntryReference).Key);
            Assert.AreEqual("Yes", DialogueTables.GetSourceText(_newOption.Text));
        }

        [Test]
        public void Paste_InvalidData_ReturnsEmpty() {
            DialogueAsset _asset = NewAsset("PasteInvalid");

            Assert.IsFalse(DialogueClipboard.CanPaste(null));
            Assert.IsFalse(DialogueClipboard.CanPaste(""));
            Assert.IsFalse(DialogueClipboard.CanPaste("not json {"));
            Assert.DoesNotThrow(() => {
                Assert.AreEqual(0, DialogueClipboard.Paste(_asset, "not json {", Vector2.zero).Count);
                Assert.AreEqual(0, DialogueClipboard.Paste(_asset, null, Vector2.zero).Count);
            });
            Assert.AreEqual(1, _asset.Nodes.Count);
        }

        [Test]
        public void Copy_OnlyStart_IsNotPasteable() {
            DialogueAsset _asset = NewAsset("CopyStart");

            string _data = DialogueClipboard.Copy(_asset, new[] { _asset.Start.Id });

            Assert.IsFalse(DialogueClipboard.CanPaste(_data));
        }
        #endregion

        #region Layout
        [Test]
        public void AutoLayout_ColumnsByDepth_RowsInOutputOrder() {
            DialogueAsset _asset = NewAsset("LayoutDepth");
            LineNode _line = (LineNode)DialogueGraphOps.CreateConnected(_asset, typeof(LineNode), Vector2.zero, _asset.Start.Next.Id);
            ChoiceNode _choice = (ChoiceNode)DialogueGraphOps.CreateConnected(_asset, typeof(ChoiceNode), Vector2.zero, _line.Next.Id);
            DialogueGraphOps.AddOption(_asset, _choice);
            DialogueNode _first = DialogueGraphOps.CreateConnected(_asset, typeof(EndNode), Vector2.zero, _choice.Options[0].Next.Id);
            DialogueNode _noAnswer = DialogueGraphOps.CreateConnected(_asset, typeof(EndNode), Vector2.zero, _choice.NoAnswer.Id);
            Assert.IsTrue(DialogueGraphOps.NeedsAutoLayout(_asset));
            Undo.IncrementCurrentGroup();

            DialogueGraphOps.AutoLayout(_asset);

            Assert.AreEqual(new Vector2(0f, 0f), _asset.Start.Position);
            Assert.AreEqual(new Vector2(350f, 0f), _line.Position);
            Assert.AreEqual(new Vector2(700f, 0f), _choice.Position);
            Assert.AreEqual(new Vector2(1050f, 0f), _first.Position);
            Assert.AreEqual(new Vector2(1050f, 200f), _noAnswer.Position);
            Assert.IsFalse(DialogueGraphOps.NeedsAutoLayout(_asset));

            // One undo step puts every node back
            UndoLast();
            Assert.AreEqual(Vector2.zero, _line.Position);
            Assert.AreEqual(Vector2.zero, _noAnswer.Position);
        }

        [Test]
        public void AutoLayout_Cycle_Terminates() {
            DialogueAsset _asset = NewAsset("LayoutCycle");
            LineNode _a = (LineNode)DialogueGraphOps.CreateConnected(_asset, typeof(LineNode), Vector2.zero, _asset.Start.Next.Id);
            LineNode _b = (LineNode)DialogueGraphOps.CreateConnected(_asset, typeof(LineNode), Vector2.zero, _a.Next.Id);
            DialogueGraphOps.Connect(_asset, _b.Next.Id, _a.Id);
            LineNode _self = (LineNode)DialogueGraphOps.CreateNode(_asset, typeof(LineNode), Vector2.zero);
            DialogueGraphOps.Connect(_asset, _self.Next.Id, _self.Id);

            DialogueGraphOps.AutoLayout(_asset);

            Assert.AreEqual(new Vector2(350f, 0f), _a.Position);
            Assert.AreEqual(new Vector2(700f, 0f), _b.Position);
            // Unreachable, so in the column after the deepest one
            Assert.AreEqual(new Vector2(1050f, 0f), _self.Position);
        }

        [Test]
        public void AutoLayout_Unreachable_InLastColumn() {
            DialogueAsset _asset = NewAsset("LayoutUnreachable");
            DialogueNode _x = DialogueGraphOps.CreateNode(_asset, typeof(LineNode), Vector2.zero);
            LineNode _reached = (LineNode)DialogueGraphOps.CreateConnected(_asset, typeof(LineNode), Vector2.zero, _asset.Start.Next.Id);
            DialogueNode _y = DialogueGraphOps.CreateNode(_asset, typeof(EndNode), Vector2.zero);

            DialogueGraphOps.AutoLayout(_asset);

            Assert.AreEqual(new Vector2(350f, 0f), _reached.Position);
            Assert.AreEqual(new Vector2(700f, 0f), _x.Position);
            Assert.AreEqual(new Vector2(700f, 200f), _y.Position);
        }

        [Test]
        public void AutoLayout_ContinueJump_HiddenOutputNotFollowed() {
            DialogueAsset _asset = NewAsset("LayoutContinueJump");
            JumpNode _jump = (JumpNode)DialogueGraphOps.CreateConnected(_asset, typeof(JumpNode), Vector2.zero, _asset.Start.Next.Id);
            LineNode _line = (LineNode)DialogueGraphOps.CreateNode(_asset, typeof(LineNode), Vector2.zero);
            DialogueNode _end = DialogueGraphOps.CreateConnected(_asset, typeof(EndNode), Vector2.zero, _line.Next.Id);
            // A link left from Call And Return mode, not shown while the jump continues
            _jump.Next.Target = _line.Id;
            Assert.AreEqual(JumpMode.Continue, _jump.Mode);

            DialogueGraphOps.AutoLayout(_asset);

            Assert.AreEqual(new Vector2(350f, 0f), _jump.Position);
            Assert.AreEqual(new Vector2(700f, 0f), _line.Position, "unreached, in the last column");
            Assert.AreEqual(new Vector2(700f, 200f), _end.Position);
        }

        [Test]
        public void NeedsAutoLayout_OnlyWhenEveryNodeAtOrigin() {
            DialogueAsset _asset = NewAsset("LayoutNeeds");
            Assert.IsFalse(DialogueGraphOps.NeedsAutoLayout(_asset), "a single node");

            DialogueNode _line = DialogueGraphOps.CreateNode(_asset, typeof(LineNode), Vector2.zero);
            Assert.IsTrue(DialogueGraphOps.NeedsAutoLayout(_asset));

            _line.Position = new Vector2(10f, 0f);
            Assert.IsFalse(DialogueGraphOps.NeedsAutoLayout(_asset));
            Assert.IsFalse(DialogueGraphOps.NeedsAutoLayout(null));
        }

        [Test]
        public void Arrange_AlignAndDistribute() {
            Dictionary<string, Rect> _rects = new Dictionary<string, Rect> {
                { "a", new Rect(0f, 0f, 100f, 50f) },
                { "b", new Rect(300f, 40f, 200f, 100f) },
                { "c", new Rect(120f, 90f, 100f, 10f) }
            };

            IReadOnlyDictionary<string, Vector2> _left = DialogueGraphOps.Arrange(_rects, DialogueGraphOps.Arrangement.Left);
            Assert.AreEqual(new Vector2(0f, 40f), _left["b"]);
            Assert.AreEqual(new Vector2(0f, 90f), _left["c"]);

            IReadOnlyDictionary<string, Vector2> _right = DialogueGraphOps.Arrange(_rects, DialogueGraphOps.Arrangement.Right);
            Assert.AreEqual(new Vector2(400f, 0f), _right["a"]);
            Assert.AreEqual(new Vector2(300f, 40f), _right["b"]);

            // Middle of the selection's bounds: y from 0 to 140
            IReadOnlyDictionary<string, Vector2> _middle = DialogueGraphOps.Arrange(_rects, DialogueGraphOps.Arrangement.Middle);
            Assert.AreEqual(new Vector2(0f, 45f), _middle["a"]);
            Assert.AreEqual(new Vector2(120f, 65f), _middle["c"]);

            // The first and last stay; between a's right edge (100) and b's left edge (300) c leaves two gaps of 50
            IReadOnlyDictionary<string, Vector2> _spread = DialogueGraphOps.Arrange(_rects, DialogueGraphOps.Arrangement.DistributeHorizontally);
            Assert.AreEqual(new Vector2(0f, 0f), _spread["a"]);
            Assert.AreEqual(new Vector2(150f, 90f), _spread["c"]);
            Assert.AreEqual(new Vector2(300f, 40f), _spread["b"]);

            // Between a's bottom (50) and c's top (90) b does not fit: the gaps are -30
            IReadOnlyDictionary<string, Vector2> _stack = DialogueGraphOps.Arrange(_rects, DialogueGraphOps.Arrangement.DistributeVertically);
            Assert.AreEqual(new Vector2(0f, 0f), _stack["a"]);
            Assert.AreEqual(new Vector2(300f, 20f), _stack["b"]);
            Assert.AreEqual(new Vector2(120f, 90f), _stack["c"]);
        }
        #endregion
    }
}
