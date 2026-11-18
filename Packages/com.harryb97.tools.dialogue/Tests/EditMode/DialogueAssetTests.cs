using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HarryTools.Dialogue.Tests {
    public class DialogueAssetTests {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string TempFolder = "Assets/__DialogueTests__";

        private readonly List<Object> created = new List<Object>();
        #endregion

        // ==================== SETUP ====================
        #region Setup
        [TearDown]
        public void TearDown() {
            DialogueSettings.ClearForTests();
            foreach (Object _object in created) {
                if (_object != null) Object.DestroyImmediate(_object);
            }
            created.Clear();

            if (AssetDatabase.IsValidFolder(TempFolder)) AssetDatabase.DeleteAsset(TempFolder);
        }

        private DialogueAsset NewAsset() {
            DialogueAsset _asset = ScriptableObject.CreateInstance<DialogueAsset>();
            created.Add(_asset);
            return _asset;
        }

        private static ChoiceNode NewChoice(DialogueAsset _asset, int _options) {
            ChoiceNode _choice = _asset.AddNode<ChoiceNode>();
            for (int _i = 0; _i < _options; _i++) _choice.Options.Add(new ChoiceOption());
            return _choice;
        }
        #endregion

        // ==================== TESTS ====================
        #region Tests
        [Test]
        public void NewAsset_HasOneStartNode() {
            DialogueAsset _asset = NewAsset();

            Assert.AreEqual(1, _asset.Nodes.Count);
            Assert.IsNotNull(_asset.Start);
            Assert.AreSame(_asset.Nodes[0], _asset.Start);
            Assert.IsNotEmpty(_asset.Start.Id);
        }

        [Test]
        public void NewAsset_StartOutputHasId() {
            DialogueAsset _asset = NewAsset();

            // The graph connects outputs by id, so a new dialogue can be connected from its start at once
            Assert.IsNotEmpty(_asset.Start.Next.Id);
        }

        [Test]
        public void AddNode_GetsUniqueId() {
            DialogueAsset _asset = NewAsset();

            LineNode _first = _asset.AddNode<LineNode>();
            LineNode _second = _asset.AddNode<LineNode>();

            Assert.IsNotEmpty(_first.Id);
            Assert.AreNotEqual(_first.Id, _second.Id);
            Assert.AreSame(_first, _asset.Find(_first.Id));
            Assert.AreEqual(3, _asset.Nodes.Count);
        }

        [Test]
        public void RemoveNode_ClearsTargetsPointingToIt() {
            DialogueAsset _asset = NewAsset();
            LineNode _line = _asset.AddNode<LineNode>();
            LineNode _other = _asset.AddNode<LineNode>();
            ChoiceNode _choice = NewChoice(_asset, 2);
            _asset.Start.Next.Target = _line.Id;
            _choice.Options[0].Next.Target = _line.Id;
            _choice.Options[1].Next.Target = _other.Id;
            _choice.NoAnswer.Target = _line.Id;

            _asset.RemoveNode(_line.Id);

            Assert.IsNull(_asset.Find(_line.Id));
            Assert.AreEqual("", _asset.Start.Next.Target);
            Assert.AreEqual("", _choice.Options[0].Next.Target);
            Assert.AreEqual("", _choice.NoAnswer.Target);
            Assert.AreEqual(_other.Id, _choice.Options[1].Next.Target);
        }

        [Test]
        public void RemoveNode_StartNode_Ignored() {
            DialogueAsset _asset = NewAsset();

            _asset.RemoveNode(_asset.Start.Id);

            Assert.IsNotNull(_asset.Start);
        }

        [Test]
        public void OnValidate_DuplicatedNodeId_Regenerated() {
            DialogueAsset _asset = NewAsset();
            LineNode _first = _asset.AddNode<LineNode>();
            LineNode _second = _asset.AddNode<LineNode>();
            string _shared = _first.Id;
            _second.Id = _shared;

            _asset.EnsureIds();

            Assert.AreEqual(_shared, _first.Id);
            Assert.IsNotEmpty(_second.Id);
            Assert.AreNotEqual(_shared, _second.Id);
        }

        [Test]
        public void OnValidate_DuplicatedOptionId_Regenerated() {
            DialogueAsset _asset = NewAsset();
            ChoiceNode _choice = NewChoice(_asset, 3);
            _asset.EnsureIds();
            string _shared = _choice.Options[0].Id;
            // The inspector's add button duplicates the last element, id included
            _choice.Options[1].Id = _shared;
            _choice.Options[2].Id = "";

            _asset.EnsureIds();

            Assert.AreEqual(_shared, _choice.Options[0].Id);
            Assert.IsNotEmpty(_choice.Options[1].Id);
            Assert.IsNotEmpty(_choice.Options[2].Id);
            Assert.AreEqual(3, new HashSet<string> { _choice.Options[0].Id, _choice.Options[1].Id, _choice.Options[2].Id }.Count);
        }

        [Test]
        public void OnValidate_EmptyOutputId_Filled() {
            DialogueAsset _asset = NewAsset();
            ChoiceNode _choice = NewChoice(_asset, 1);
            BranchNode _branch = _asset.AddNode<BranchNode>();
            _branch.Cases.Add(new BranchCase());

            _asset.EnsureIds();

            Assert.IsNotEmpty(_asset.Start.Next.Id);
            Assert.IsNotEmpty(_choice.Options[0].Next.Id);
            Assert.IsNotEmpty(_choice.NoAnswer.Id);
            Assert.IsNotEmpty(_branch.Cases[0].Next.Id);
            Assert.IsNotEmpty(_branch.Otherwise.Id);
        }

        [Test]
        public void CollectOutputs_Choice_OptionsThenNoAnswer() {
            DialogueAsset _asset = NewAsset();
            ChoiceNode _choice = NewChoice(_asset, 2);
            List<NodeOutput> _outputs = new List<NodeOutput>();

            _choice.CollectOutputs(_outputs);

            Assert.AreEqual(3, _outputs.Count);
            Assert.AreSame(_choice.Options[0].Next, _outputs[0]);
            Assert.AreSame(_choice.Options[1].Next, _outputs[1]);
            Assert.AreSame(_choice.NoAnswer, _outputs[2]);
        }

        [Test]
        public void CollectOutputs_Branch_CasesThenOtherwise() {
            DialogueAsset _asset = NewAsset();
            BranchNode _branch = _asset.AddNode<BranchNode>();
            _branch.Cases.Add(new BranchCase());
            _branch.Cases.Add(new BranchCase());
            List<NodeOutput> _outputs = new List<NodeOutput>();

            _branch.CollectOutputs(_outputs);

            Assert.AreEqual(3, _outputs.Count);
            Assert.AreSame(_branch.Cases[0].Next, _outputs[0]);
            Assert.AreSame(_branch.Cases[1].Next, _outputs[1]);
            Assert.AreSame(_branch.Otherwise, _outputs[2]);
        }

        [Test]
        public void LineLifetime_Resolve() {
            LineLifetime _fixed = new LineLifetime { Mode = LineLifetimeMode.Fixed, Seconds = 3f };
            LineLifetime _byLength = new LineLifetime { Mode = LineLifetimeMode.ByTextLength, Base = 1.5f, PerCharacter = 0.05f };
            LineLifetime _byVoice = new LineLifetime { Mode = LineLifetimeMode.ByVoice, Base = 1.5f, PerCharacter = 0.05f };
            LineLifetime _untilNext = new LineLifetime { Mode = LineLifetimeMode.UntilNext };

            Assert.AreEqual(3f, _fixed.Resolve(20), 0.0001f);
            Assert.AreEqual(2.5f, _byLength.Resolve(20), 0.0001f);
            Assert.AreEqual(2.5f, _byVoice.Resolve(20), 0.0001f);
            Assert.IsTrue(float.IsPositiveInfinity(_untilNext.Resolve(20)));
        }

        [Test]
        public void Asset_AssetIdWhenEmpty_IsStableInMemoryId() {
            DialogueAsset _asset = NewAsset();

            string _first = _asset.AssetId;

            Assert.IsNotEmpty(_first);
            Assert.AreEqual(_first, _asset.AssetId);
        }

        [Test]
        public void Settings_NewInstance_Defaults() {
            // A fresh instance, so the project's own settings asset does not change the result
            DialogueSettings _settings = ScriptableObject.CreateInstance<DialogueSettings>();

            Assert.AreEqual(DialogueTime.Unscaled, _settings.DefaultTime);
            Assert.AreEqual(AmbientPolicy.Continue, _settings.DefaultAmbientPolicy);
            Assert.AreEqual(LineLifetimeMode.ByTextLength, _settings.DefaultLifetime.Mode);
            Assert.AreEqual(AdvanceMode.Both, _settings.DefaultAdvance);
            Assert.IsFalse(_settings.HistoryEnabled);
            Assert.AreEqual(200, _settings.HistoryMax);
            Assert.AreEqual(HistoryClear.OnConversationEnd, _settings.HistoryClear);
            Assert.AreEqual("Assets/Localization/Dialogue", _settings.TablesFolder);
            Assert.AreEqual("", _settings.SourceLocaleCode);
            Object.DestroyImmediate(_settings);
        }

        [Test]
        public void Settings_UseForTests_IsCurrent() {
            DialogueSettings _settings = ScriptableObject.CreateInstance<DialogueSettings>();
            created.Add(_settings);
            DialogueSettings.UseForTests(_settings);

            Assert.AreSame(_settings, DialogueSettings.Current);
        }

        [Test]
        public void AssetIdPostprocessor_SetsGuid_DuplicateGetsNew() {
            Directory.CreateDirectory(TempFolder);
            string _pathA = TempFolder + "/A.asset";
            string _pathB = TempFolder + "/B.asset";
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<DialogueAsset>(), _pathA);
            AssetDatabase.ImportAsset(_pathA, ImportAssetOptions.ForceUpdate);

            DialogueAsset _a = AssetDatabase.LoadAssetAtPath<DialogueAsset>(_pathA);
            string _guidA = AssetDatabase.AssetPathToGUID(_pathA);
            Assert.AreEqual(_guidA, _a.AssetId);

            Assert.IsTrue(AssetDatabase.CopyAsset(_pathA, _pathB));
            AssetDatabase.ImportAsset(_pathB, ImportAssetOptions.ForceUpdate);
            DialogueAsset _b = AssetDatabase.LoadAssetAtPath<DialogueAsset>(_pathB);
            string _guidB = AssetDatabase.AssetPathToGUID(_pathB);

            Assert.AreNotEqual(_guidA, _guidB);
            Assert.AreEqual(_guidB, _b.AssetId);
            Assert.AreEqual(_guidA, _a.AssetId);

            string _pathC = TempFolder + "/C.asset";
            Assert.AreEqual("", AssetDatabase.MoveAsset(_pathA, _pathC));
            AssetDatabase.ImportAsset(_pathC, ImportAssetOptions.ForceUpdate);
            Assert.AreEqual(_guidA, AssetDatabase.LoadAssetAtPath<DialogueAsset>(_pathC).AssetId);
        }

        // ==================== GROUPS AND NOTES ====================
        #region Groups And Notes
        [Test]
        public void RemoveNode_RemovesFromGroups() {
            DialogueAsset _asset = NewAsset();
            LineNode _line = _asset.AddNode<LineNode>();
            LineNode _other = _asset.AddNode<LineNode>();
            DialogueGroup _group = new DialogueGroup();
            _group.NodeIdList.Add(_line.Id);
            _group.NodeIdList.Add(_other.Id);
            _asset.GroupList.Add(_group);

            _asset.RemoveNode(_line.Id);

            CollectionAssert.AreEqual(new[] { _other.Id }, _group.NodeIds);
        }

        [Test]
        public void EnsureIds_GroupAndNoteIds_Regenerated() {
            DialogueAsset _asset = NewAsset();
            DialogueGroup _empty = new DialogueGroup();
            DialogueGroup _first = new DialogueGroup { Id = "same" };
            DialogueGroup _repeated = new DialogueGroup { Id = "same" };
            DialogueNote _noteEmpty = new DialogueNote();
            DialogueNote _noteFirst = new DialogueNote { Id = "note" };
            DialogueNote _noteRepeated = new DialogueNote { Id = "note" };
            _asset.GroupList.AddRange(new[] { _empty, _first, _repeated });
            _asset.NoteList.AddRange(new[] { _noteEmpty, _noteFirst, _noteRepeated });

            _asset.EnsureIds();

            Assert.AreEqual("same", _first.Id);
            Assert.AreEqual("note", _noteFirst.Id);
            string[] _ids = { _empty.Id, _first.Id, _repeated.Id, _noteEmpty.Id, _noteFirst.Id, _noteRepeated.Id, _asset.Start.Id };
            foreach (string _id in _ids) Assert.IsNotEmpty(_id);
            Assert.AreEqual(_ids.Length, new HashSet<string>(_ids).Count);
        }

        [Test]
        public void RegenerateIds_GroupsFollowNewNodeIds() {
            DialogueAsset _asset = NewAsset();
            LineNode _line = _asset.AddNode<LineNode>();
            string _oldLineId = _line.Id;
            DialogueGroup _group = new DialogueGroup { Id = "group" };
            _group.NodeIdList.Add(_line.Id);
            _group.NodeIdList.Add("missing");
            _asset.GroupList.Add(_group);
            _asset.NoteList.Add(new DialogueNote { Id = "note" });

            _asset.RegenerateIds();

            Assert.AreNotEqual(_oldLineId, _line.Id);
            CollectionAssert.AreEqual(new[] { _line.Id }, _group.NodeIds);
            Assert.AreNotEqual("group", _group.Id);
            Assert.AreNotEqual("note", _asset.Notes[0].Id);
        }
        #endregion
        #endregion
    }
}
