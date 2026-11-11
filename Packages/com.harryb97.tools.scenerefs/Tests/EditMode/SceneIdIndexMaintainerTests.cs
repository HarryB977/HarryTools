using System.Collections.Generic;
using HarryTools.SceneRefs.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

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
        public void PendingImports_SurviveDomainReload()
        {
            Scene _scene = util.NewScene("Survive");
            SceneIndexTestUtil.AddId(SceneIndexTestUtil.NewObject(_scene, "Kept"), GuidA, "Kept");
            SceneIndexTestUtil.Save(_scene);
            string _sceneGuid = AssetDatabase.AssetPathToGUID(SceneIndexTestUtil.ScenePath("Survive"));
            util.Close(_scene);
            SceneIdIndexMaintainer.FlushPending();
            util.Index.RemoveScene(_sceneGuid);

            AssetDatabase.ImportAsset(SceneIndexTestUtil.ScenePath("Survive"), ImportAssetOptions.ForceUpdate);
            // What a domain reload (entering or leaving Play) does to the queue in memory.
            SceneIdIndexMaintainer.ForgetInMemoryForTests();
            SceneIdIndexMaintainer.FlushPending();

            List<SceneIdIndexEntry> _entries = util.EntriesOf(_sceneGuid);
            Assert.AreEqual(1, _entries.Count, "The queued import must survive the reload.");
            Assert.AreEqual("Kept", _entries[0].Id);
        }

        [Test]
        public void Rebuild_IndexesClosedScenes_AndPrefabsWithIds()
        {
            Scene _scene = util.NewScene("Rebuild");
            SceneIndexTestUtil.AddId(SceneIndexTestUtil.NewObject(_scene, "Closed"), GuidA, "Closed");
            SceneIndexTestUtil.Save(_scene);
            string _sceneGuid = AssetDatabase.AssetPathToGUID(SceneIndexTestUtil.ScenePath("Rebuild"));
            util.Close(_scene);

            string _prefabPath = SceneIndexTestUtil.Folder + "/WithId.prefab";
            string _noIdPath = SceneIndexTestUtil.Folder + "/NoId.prefab";
            try
            {
                // The prefab keeps its SceneId on purpose, so the stripper is held back while it is saved.
                using (PrefabIdStripper.Suspend())
                {
                    Scene _scratch = util.NewScene("Scratch");
                    GameObject _prefabSource = SceneIndexTestUtil.NewObject(_scratch, "WithId");
                    SceneIndexTestUtil.AddId(SceneIndexTestUtil.NewObject(_scratch, "Inner", _prefabSource), GuidB, "Inner");
                    PrefabUtility.SaveAsPrefabAsset(_prefabSource, _prefabPath);
                    PrefabUtility.SaveAsPrefabAsset(SceneIndexTestUtil.NewObject(_scratch, "NoId"), _noIdPath);
                    util.Close(_scratch);
                }

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
            finally
            {
                AssetDatabase.DeleteAsset(_prefabPath);
                AssetDatabase.DeleteAsset(_noIdPath);
            }
        }

        [Test]
        public void Rebuild_CorruptScene_ContinuesAndIndexesGoodScene()
        {
            Scene _scene = util.NewScene("Good");
            SceneIndexTestUtil.AddId(SceneIndexTestUtil.NewObject(_scene, "Good"), GuidA, "Good");
            SceneIndexTestUtil.Save(_scene);
            string _goodGuid = AssetDatabase.AssetPathToGUID(SceneIndexTestUtil.ScenePath("Good"));
            util.Close(_scene);

            // Unity logs import and load errors for the broken files; they are expected here.
            LogAssert.ignoreFailingMessages = true;

            // Names sort before "Good" so the broken scenes are read first.
            string _corruptPath = SceneIndexTestUtil.ScenePath("Corrupt");
            System.IO.File.WriteAllText(_corruptPath, "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!1 &1\nGameObject: [ this is not\n  {{ valid\n");
            System.IO.File.WriteAllText(SceneIndexTestUtil.Folder + "/AAA_Garbage.unity", "\u0000\u0001 not a scene at all");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            SceneIdIndexMaintainer.FlushPending();
            int _sceneCount = SceneManager.sceneCount;

            bool _completed = SceneIdIndexMaintainer.Rebuild(false, new[] { SceneIndexTestUtil.Folder });

            Assert.IsTrue(_completed);
            Assert.AreEqual(_sceneCount, SceneManager.sceneCount, "Rebuild must close every scene it opened.");
            List<SceneIdIndexEntry> _entries = util.EntriesOf(_goodGuid);
            Assert.AreEqual(1, _entries.Count);
            Assert.AreEqual("Good", _entries[0].Id);
        }

        [Test]
        public void DestroyedOverride_NeverTouchesProjectIndex()
        {
            bool _existedBefore = System.IO.File.Exists(SceneIdIndex.DefaultPath);
            SceneIdIndex _doomed = ScriptableObject.CreateInstance<SceneIdIndex>();
            SceneIdIndex.Active = _doomed;
            Object.DestroyImmediate(_doomed);
            try
            {
                Assert.IsTrue(SceneIdIndex.Active == null);
                Assert.IsFalse(SceneIdIndexMaintainer.Rebuild(false, new[] { SceneIndexTestUtil.Folder }));

                Scene _scene = util.NewScene("Orphan");
                SceneIndexTestUtil.AddId(SceneIndexTestUtil.NewObject(_scene, "Orphan"), GuidA, "Orphan");
                SceneIndexTestUtil.Save(_scene);
                SceneIdIndexMaintainer.FlushPending();

                if (!_existedBefore) Assert.IsFalse(System.IO.File.Exists(SceneIdIndex.DefaultPath), "The project index must not be created while a test override is set.");
            }
            finally
            {
                SceneIdIndex.Active = util.Index;
            }
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
