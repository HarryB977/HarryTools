using System.Collections.Generic;
using HarryTools.SceneRefs.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HarryTools.SceneRefs.Tests
{
    public class SceneIdIndexMaintainerTests
    {
        // ==================== VARIABLES ====================

        private const string GuidA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string GuidB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private const string GuidC = "cccccccccccccccccccccccccccccccc";

        private SceneIndexTestUtil util;

        // ==================== SETUP ====================

        [SetUp]
        public void SetUp()
        {
            util = new SceneIndexTestUtil();
            util.SetUp();
        }

        [TearDown]
        public void TearDown() => util.TearDown();

        // ==================== TESTS ====================

        [Test]
        public void SceneSaved_ReplacesEntries()
        {
            Scene _scene = util.NewScene("Saved");
            SceneId _first = SceneIndexTestUtil.AddId(SceneIndexTestUtil.NewObject(_scene, "First"), GuidA, "First");
            SceneIndexTestUtil.Save(_scene);
            string _sceneGuid = AssetDatabase.AssetPathToGUID(SceneIndexTestUtil.ScenePath("Saved"));

            List<SceneIdIndexEntry> _entries = util.EntriesOf(_sceneGuid);
            Assert.AreEqual(1, _entries.Count);
            Assert.AreEqual("First", _entries[0].Id);

            _first.Set(GuidA, "Renamed");
            SceneIndexTestUtil.AddId(SceneIndexTestUtil.NewObject(_scene, "Second"), GuidB, "Second");
            EditorSceneManager.MarkSceneDirty(_scene);
            EditorSceneManager.SaveScene(_scene);
            SceneIdIndexMaintainer.FlushPending();

            _entries = util.EntriesOf(_sceneGuid);
            Assert.AreEqual(2, _entries.Count);
            Assert.AreEqual("Renamed", _entries[0].Id);
            Assert.AreEqual("Second", _entries[1].Id);
            // The import that follows the save must not downgrade the exact entries to the light ones.
            Assert.IsNotNull(_entries[0].ComponentTypes);
            Assert.IsNotNull(_entries[1].ComponentTypes);
        }

        [Test]
        public void SceneDeleted_RemovesEntries()
        {
            Scene _scene = util.NewScene("Deleted");
            SceneIndexTestUtil.AddId(SceneIndexTestUtil.NewObject(_scene, "Doomed"), GuidA, "Doomed");
            SceneIndexTestUtil.Save(_scene);
            string _sceneGuid = AssetDatabase.AssetPathToGUID(SceneIndexTestUtil.ScenePath("Deleted"));
            util.Close(_scene);
            SceneIdIndexMaintainer.FlushPending();
            Assert.AreEqual(1, util.EntriesOf(_sceneGuid).Count);

            AssetDatabase.DeleteAsset(SceneIndexTestUtil.ScenePath("Deleted"));
            SceneIdIndexMaintainer.FlushPending();

            Assert.AreEqual(0, util.EntriesOf(_sceneGuid).Count);
        }

        [Test]
        public void ExternalChange_ReindexesClosedSceneFromFile()
        {
            Scene _scene = util.NewScene("External");
            SceneIndexTestUtil.AddId(SceneIndexTestUtil.NewObject(_scene, "Closed"), GuidA, "Closed");
            SceneIndexTestUtil.Save(_scene);
            string _sceneGuid = AssetDatabase.AssetPathToGUID(SceneIndexTestUtil.ScenePath("External"));
            util.Close(_scene);
            util.Index.RemoveScene(_sceneGuid);

            AssetDatabase.ImportAsset(SceneIndexTestUtil.ScenePath("External"), ImportAssetOptions.ForceUpdate);
            CollectionAssert.Contains(util.Index.StaleScenes, _sceneGuid);
            SceneIdIndexMaintainer.FlushPending();

            List<SceneIdIndexEntry> _entries = util.EntriesOf(_sceneGuid);
            Assert.AreEqual(1, _entries.Count);
            Assert.AreEqual("Closed", _entries[0].Id);
            Assert.IsNull(_entries[0].ComponentTypes);
            CollectionAssert.DoesNotContain(util.Index.StaleScenes, _sceneGuid);
        }

        [Test]
        public void Rebuild_IndexesClosedScenes_AndPrefabsWithIds()
        {
            Scene _scene = util.NewScene("Rebuild");
            SceneIndexTestUtil.AddId(SceneIndexTestUtil.NewObject(_scene, "Closed"), GuidA, "Closed");
            SceneIndexTestUtil.Save(_scene);
            string _sceneGuid = AssetDatabase.AssetPathToGUID(SceneIndexTestUtil.ScenePath("Rebuild"));
            util.Close(_scene);

            Scene _scratch = util.NewScene("Scratch");
            GameObject _prefabSource = SceneIndexTestUtil.NewObject(_scratch, "WithId");
            SceneIndexTestUtil.AddId(SceneIndexTestUtil.NewObject(_scratch, "Inner", _prefabSource), GuidB, "Inner");
            string _prefabPath = SceneIndexTestUtil.Folder + "/WithId.prefab";
            PrefabUtility.SaveAsPrefabAsset(_prefabSource, _prefabPath);
            PrefabUtility.SaveAsPrefabAsset(SceneIndexTestUtil.NewObject(_scratch, "NoId"), SceneIndexTestUtil.Folder + "/NoId.prefab");
            util.Close(_scratch);

            int _sceneCount = SceneManager.sceneCount;
            util.Index.ReplaceScene("stale-scene", new List<SceneIdIndexEntry> { new SceneIdIndexEntry { Guid = GuidC, Id = "Gone", SceneGuid = "stale-scene" } });
            SceneIdIndexMaintainer.FlushPending();

            bool _rebuilt = false;
            System.Action _onRebuilt = () => _rebuilt = true;
            SceneIdIndexMaintainer.Rebuilt += _onRebuilt;
            try { SceneIdIndexMaintainer.Rebuild(false, new[] { SceneIndexTestUtil.Folder }); }
            finally { SceneIdIndexMaintainer.Rebuilt -= _onRebuilt; }

            Assert.IsTrue(_rebuilt);
            Assert.AreEqual(_sceneCount, SceneManager.sceneCount, "Rebuild must close the scenes it opens.");
            List<SceneIdIndexEntry> _entries = util.EntriesOf(_sceneGuid);
            Assert.AreEqual(1, _entries.Count);
            Assert.AreEqual(GuidA, _entries[0].Guid);
            Assert.IsNotNull(_entries[0].ComponentTypes, "Rebuild uses the exact path.");
            Assert.AreEqual(0, util.EntriesOf("stale-scene").Count);
            CollectionAssert.AreEqual(new[] { _prefabPath }, util.Index.PrefabsWithIds);
        }

        [Test]
        public void Entries_AreSorted()
        {
            SceneIdIndex _index = util.Index;
            _index.ReplaceScene("b", new List<SceneIdIndexEntry> { Entry("b", "z"), Entry("b", "a") });
            _index.ReplaceScene("a", new List<SceneIdIndexEntry> { Entry("a", "m") });

            Assert.AreEqual(3, _index.Entries.Count);
            Assert.AreEqual("a/m", _index.Entries[0].SceneGuid + "/" + _index.Entries[0].Guid);
            Assert.AreEqual("b/a", _index.Entries[1].SceneGuid + "/" + _index.Entries[1].Guid);
            Assert.AreEqual("b/z", _index.Entries[2].SceneGuid + "/" + _index.Entries[2].Guid);
        }

        [Test]
        public void Active_UsesTestOverride()
        {
            Assert.AreSame(util.Index, SceneIdIndex.Active);
        }

        // ==================== HELPERS ====================

        private static SceneIdIndexEntry Entry(string _sceneGuid, string _guid) =>
            new SceneIdIndexEntry { SceneGuid = _sceneGuid, Guid = _guid, Id = _guid };
    }
}
