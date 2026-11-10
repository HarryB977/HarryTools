using System.Collections.Generic;
using HarryTools.SceneRefs.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HarryTools.SceneRefs.Tests
{
    public class SceneIndexerTests
    {
        // ==================== VARIABLES ====================

        private const string ChildGuid = "0123456789abcdef0123456789abcdef";

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
        public void FromLoadedScene_ReadsGuidIdPathAndTypes()
        {
            Scene _scene = util.NewScene("Indexer");
            GameObject _root = SceneIndexTestUtil.NewObject(_scene, "Root");
            GameObject _child = SceneIndexTestUtil.NewObject(_scene, "Child", _root);
            _child.AddComponent<Light>();
            SceneIndexTestUtil.AddId(_child, ChildGuid, "Lamp");
            SceneIndexTestUtil.Save(_scene);

            List<SceneIdIndexEntry> _entries = SceneIndexer.FromLoadedScene(_scene);

            Assert.AreEqual(1, _entries.Count);
            SceneIdIndexEntry _entry = _entries[0];
            Assert.AreEqual(ChildGuid, _entry.Guid);
            Assert.AreEqual("Lamp", _entry.Id);
            Assert.AreEqual(AssetDatabase.AssetPathToGUID(SceneIndexTestUtil.ScenePath("Indexer")), _entry.SceneGuid);
            Assert.AreEqual("Indexer", _entry.SceneName);
            Assert.AreEqual("Root/Child", _entry.HierarchyPath);
            Assert.IsNotNull(_entry.ComponentTypes);
            CollectionAssert.Contains(_entry.ComponentTypes, "UnityEngine.Light");
            CollectionAssert.DoesNotContain(_entry.ComponentTypes, "UnityEngine.Transform");
            CollectionAssert.DoesNotContain(_entry.ComponentTypes, typeof(SceneId).FullName);
        }

        [Test]
        public void FromLoadedScene_UnsavedScene_UsesHandleKey()
        {
            Scene _scene = SceneIndexTestUtil.UntitledScene();
            if (!_scene.IsValid()) Assert.Ignore("No untitled scene loaded.");
            GameObject _item = SceneIndexTestUtil.NewObject(_scene, "Item");
            try
            {
                SceneIndexTestUtil.AddId(_item, ChildGuid, "Item");

                List<SceneIdIndexEntry> _entries = SceneIndexer.FromLoadedScene(_scene);

                Assert.AreEqual(1, _entries.Count);
                Assert.AreEqual("unsaved:" + _scene.handle, _entries[0].SceneGuid);
            }
            finally
            {
                Object.DestroyImmediate(_item);
            }
        }
    }
}
