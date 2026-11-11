using System.Collections.Generic;
using HarryTools.SceneRefs.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HarryTools.SceneRefs.Tests
{
    public class SceneRefSyncTests
    {
        // ==================== VARIABLES ====================

        private const string GuidA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

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

        // ==================== HELPERS ====================

        private static string TypeName<T>() => $"{typeof(T).FullName}, {typeof(T).Assembly.GetName().Name}";

        private static void SyncAll(Object _owner)
        {
            SerializedObject _so = new SerializedObject(_owner);
            foreach (SerializedProperty _property in SceneRefPropertyWalker.FindAll(_so)) SceneRefSync.SyncProperty(_property);
        }

        // ==================== SYNC ====================

        [Test]
        public void Sync_SameScene_WritesGuidTypeIndexLabel()
        {
            Scene _scene = util.NewScene("SyncSame");
            GameObject _target = SceneIndexTestUtil.NewObject(_scene, "Lamp Post");
            _target.AddComponent<AudioSource>();
            AudioSource _second = _target.AddComponent<AudioSource>();
            SceneRefHolder _holder = SceneIndexTestUtil.NewObject(_scene, "Holder").AddComponent<SceneRefHolder>();
            _holder.audioSource.SetForTests(_second, null, null, 0, null);

            SyncAll(_holder);

            SceneId _id = _target.GetComponent<SceneId>();
            Assert.IsNotNull(_id);
            Assert.AreEqual("Lamp_Post", _id.Id);
            Assert.AreEqual(_id.Guid, _holder.audioSource.guid);
            Assert.AreEqual(_second, _holder.audioSource.direct);
            Assert.AreEqual(TypeName<AudioSource>(), _holder.audioSource.componentType);
            Assert.AreEqual(1, _holder.audioSource.componentIndex);
            Assert.AreEqual("Lamp_Post in SyncSame", _holder.audioSource.label);
        }

        [Test]
        public void Sync_CrossSceneDirect_ClearedKeepsGuid()
        {
            Scene _a = util.NewScene("CrossA");
            Scene _b = util.NewScene("CrossB");
            GameObject _target = SceneIndexTestUtil.NewObject(_b, "Target");
            SceneIndexTestUtil.AddId(_target, GuidA, "Target");
            SceneRefHolder _holder = SceneIndexTestUtil.NewObject(_a, "Holder").AddComponent<SceneRefHolder>();
            _holder.target.SetForTests(_target, GuidA, string.Empty, 0, "Target in CrossB");

            SceneRefSync.SyncScene(_a);

            Assert.IsTrue(_holder.target.direct == null);
            Assert.AreEqual(GuidA, _holder.target.guid);
            Assert.AreEqual("Target in CrossB", _holder.target.label);
        }

        [Test]
        public void Sync_FindsRefsInsideSerializeReferenceLists()
        {
            Scene _scene = util.NewScene("SyncList");
            GameObject _target = SceneIndexTestUtil.NewObject(_scene, "Door");
            SceneRefListHolder _holder = SceneIndexTestUtil.NewObject(_scene, "Holder").AddComponent<SceneRefListHolder>();
            SceneRefListItem _item = new SceneRefListItem();
            _item.target.SetForTests(_target, null, null, 0, null);
            _holder.items.Add(_item);

            List<SerializedProperty> _found = new List<SerializedProperty>(SceneRefPropertyWalker.FindAll(new SerializedObject(_holder)));
            Assert.AreEqual(1, _found.Count);

            SceneRefSync.SyncScene(_scene);

            SceneRefListItem _synced = (SceneRefListItem)_holder.items[0];
            SceneId _id = _target.GetComponent<SceneId>();
            Assert.IsNotNull(_id);
            Assert.AreEqual(_id.Guid, _synced.target.guid);
            Assert.AreEqual("Door in SyncList", _synced.target.label);
        }

        [Test]
        public void RemoveEarlierSibling_SaveResyncsComponentIndex()
        {
            Scene _scene = util.NewScene("SyncRemove");
            GameObject _target = SceneIndexTestUtil.NewObject(_scene, "Speaker");
            AudioSource _first = _target.AddComponent<AudioSource>();
            AudioSource _second = _target.AddComponent<AudioSource>();
            SceneRefHolder _holder = SceneIndexTestUtil.NewObject(_scene, "Holder").AddComponent<SceneRefHolder>();
            _holder.audioSource.SetForTests(_second, null, null, 0, null);
            SceneIndexTestUtil.Save(_scene);
            Assert.AreEqual(1, _holder.audioSource.componentIndex);

            Object.DestroyImmediate(_first);
            SceneIndexTestUtil.Save(_scene);

            Assert.AreEqual(_second, _holder.audioSource.direct);
            Assert.AreEqual(0, _holder.audioSource.componentIndex);
            Assert.AreEqual(_target.GetComponent<SceneId>().Guid, _holder.audioSource.guid);
        }

        [Test]
        public void Sync_InsideSamePrefabInstance_DirectOnly_NoSceneId()
        {
            Scene _scene = util.NewScene("SyncInstance");
            GameObject _root = SceneIndexTestUtil.NewObject(_scene, "Enemy");
            GameObject _muzzle = SceneIndexTestUtil.NewObject(_scene, "Muzzle", _root);
            SceneRefHolder _holder = _root.AddComponent<SceneRefHolder>();
            PrefabUtility.SaveAsPrefabAssetAndConnect(_root, SceneIndexTestUtil.Folder + "/Enemy.prefab", InteractionMode.AutomatedAction);
            // Id fields an older sync may have left behind are cleared too.
            _holder.target.SetForTests(_muzzle, GuidA, "x", 1, "Muzzle in SyncInstance");

            SceneRefSync.SyncScene(_scene);

            Assert.IsNull(_muzzle.GetComponent<SceneId>(), "An internal reference of a prefab instance must not add a SceneId.");
            Assert.AreEqual(_muzzle, _holder.target.direct);
            Assert.AreEqual(string.Empty, _holder.target.guid);
            Assert.AreEqual(string.Empty, _holder.target.componentType);
            Assert.AreEqual(0, _holder.target.componentIndex);
            Assert.AreEqual(string.Empty, _holder.target.label);
        }

        [Test]
        public void Sync_FromPrefabInstanceToOtherObject_KeepsGuid()
        {
            Scene _scene = util.NewScene("SyncInstanceOut");
            GameObject _root = SceneIndexTestUtil.NewObject(_scene, "Enemy");
            SceneRefHolder _holder = _root.AddComponent<SceneRefHolder>();
            PrefabUtility.SaveAsPrefabAssetAndConnect(_root, SceneIndexTestUtil.Folder + "/EnemyOut.prefab", InteractionMode.AutomatedAction);
            GameObject _door = SceneIndexTestUtil.NewObject(_scene, "Door");
            _holder.target.SetForTests(_door, null, null, 0, null);

            SceneRefSync.SyncScene(_scene);

            SceneId _id = _door.GetComponent<SceneId>();
            Assert.IsNotNull(_id);
            Assert.AreEqual(_id.Guid, _holder.target.guid);
            Assert.AreEqual(_door, _holder.target.direct);
        }

        // ==================== ENSURE ID ====================

        [Test]
        public void EnsureId_UsesSanitizedNameAndIsUnique()
        {
            Scene _scene = util.NewScene("Ensure");
            SceneIndexTestUtil.AddId(SceneIndexTestUtil.NewObject(_scene, "Other"), GuidA, "Puerta_1");
            // A closed scene known only through the index, with a different case.
            util.Index.ReplaceScene("closedscene", new List<SceneIdIndexEntry>
            {
                new SceneIdIndexEntry { Guid = "cccccccccccccccccccccccccccccccc", Id = "puerta_2", SceneName = "Closed", HierarchyPath = "Puerta" },
            });
            GameObject _go = SceneIndexTestUtil.NewObject(_scene, "Puerta (1)");

            SceneId _id = SceneRefSync.EnsureId(_go);

            Assert.IsNotNull(_id);
            Assert.AreEqual("Puerta_3", _id.Id);
            Assert.AreEqual(32, _id.Guid.Length);
            Assert.AreSame(_id, SceneRefSync.EnsureId(_go));
            Assert.AreEqual("Puerta_3", _id.Id);
        }

        [Test]
        public void EnsureId_InPrefabAsset_DoesNothing()
        {
            Scene _scene = util.NewScene("EnsurePrefab");
            GameObject _source = SceneIndexTestUtil.NewObject(_scene, "Thing");
            string _path = SceneIndexTestUtil.Folder + "/Thing.prefab";
            GameObject _asset = PrefabUtility.SaveAsPrefabAsset(_source, _path);
            Object.DestroyImmediate(_source);

            Assert.IsNull(SceneRefSync.EnsureId(_asset));
            Assert.IsNull(_asset.GetComponent<SceneId>());

            // Prefab contents loaded for editing live in a preview scene, like Prefab Mode.
            GameObject _contents = PrefabUtility.LoadPrefabContents(_path);
            try
            {
                Assert.IsNull(SceneRefSync.EnsureId(_contents));
                Assert.IsNull(_contents.GetComponent<SceneId>());
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(_contents);
            }
        }
    }
}
