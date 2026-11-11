using System.Collections;
using System.Text.RegularExpressions;
using HarryTools.SceneRefs.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HarryTools.SceneRefs.Tests
{
    public class PrefabIdStripperTests
    {
        // ==================== VARIABLES ====================

        private const string GuidA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string GuidB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        // Postprocessor delayCall, strip, re-add delayCall and some margin.
        private const int FramesToSettle = 10;

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

        [UnityTest]
        public IEnumerator CreatePrefabFromSceneObject_StripsAsset_InstanceKeepsSameGuidAndId()
        {
            Scene _scene = util.NewScene("FromScene");
            GameObject _go = SceneIndexTestUtil.NewObject(_scene, "Door");
            SceneIndexTestUtil.AddId(_go, GuidA, "Door");
            string _path = PrefabPath("Door");

            PrefabUtility.SaveAsPrefabAssetAndConnect(_go, _path, InteractionMode.AutomatedAction);
            yield return Settle();

            Assert.AreEqual(0, AssetIdCount(_path), "The prefab asset must not keep the SceneId.");
            SceneId _id = _go.GetComponent<SceneId>();
            Assert.IsNotNull(_id, "The scene instance must keep its SceneId.");
            Assert.AreEqual(GuidA, _id.Guid);
            Assert.AreEqual("Door", _id.Id);
            Assert.IsTrue(PrefabUtility.IsAddedComponentOverride(_id), "The SceneId must be an added component of the instance.");
        }

        [UnityTest]
        public IEnumerator ApplyIdOverride_StripsAsset_InstanceKeepsId()
        {
            Scene _scene = util.NewScene("Apply");
            GameObject _go = SceneIndexTestUtil.NewObject(_scene, "Lamp");
            string _path = PrefabPath("Lamp");
            PrefabUtility.SaveAsPrefabAssetAndConnect(_go, _path, InteractionMode.AutomatedAction);
            yield return Settle();

            SceneId _added = SceneIndexTestUtil.AddId(_go, GuidB, "Lamp");
            PrefabUtility.ApplyAddedComponent(_added, _path, InteractionMode.AutomatedAction);
            yield return Settle();

            Assert.AreEqual(0, AssetIdCount(_path));
            SceneId _id = _go.GetComponent<SceneId>();
            Assert.IsNotNull(_id);
            Assert.AreEqual(GuidB, _id.Guid);
            Assert.AreEqual("Lamp", _id.Id);
            Assert.IsTrue(PrefabUtility.IsAddedComponentOverride(_id));
        }

        [UnityTest]
        public IEnumerator NewInstanceOfStrippedPrefab_HasNoId()
        {
            Scene _scene = util.NewScene("NewInstance");
            GameObject _go = SceneIndexTestUtil.NewObject(_scene, "Crate");
            SceneIndexTestUtil.AddId(_go, GuidA, "Crate");
            string _path = PrefabPath("Crate");
            PrefabUtility.SaveAsPrefabAssetAndConnect(_go, _path, InteractionMode.AutomatedAction);
            yield return Settle();

            GameObject _copy = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(_path), _scene);

            Assert.IsNull(_copy.GetComponentInChildren<SceneId>(true));
            Assert.AreEqual(GuidA, _go.GetComponent<SceneId>().Guid, "The original instance keeps its id.");
        }

        [UnityTest]
        public IEnumerator NestedChildWithId_AlsoStripped()
        {
            Scene _scene = util.NewScene("Nested");
            GameObject _root = SceneIndexTestUtil.NewObject(_scene, "Car");
            GameObject _child = SceneIndexTestUtil.NewObject(_scene, "Body", _root);
            GameObject _grandChild = SceneIndexTestUtil.NewObject(_scene, "Wheel", _child);
            SceneIndexTestUtil.AddId(_grandChild, GuidA, "Wheel");
            string _path = PrefabPath("Car");

            PrefabUtility.SaveAsPrefabAssetAndConnect(_root, _path, InteractionMode.AutomatedAction);
            yield return Settle();

            Assert.AreEqual(0, AssetIdCount(_path));
            SceneId _id = _grandChild.GetComponent<SceneId>();
            Assert.IsNotNull(_id);
            Assert.AreEqual(GuidA, _id.Guid);
            Assert.AreEqual("Wheel", _id.Id);
        }

        [UnityTest]
        public IEnumerator SharedGuidAcrossInstances_FirstKeepsId_OthersWarn()
        {
            Scene _scene = util.NewScene("Shared");
            string _path = PrefabPath("Shared");
            using (PrefabIdStripper.Suspend())
            {
                GameObject _source = SceneIndexTestUtil.NewObject(_scene, "Shared");
                SceneIndexTestUtil.AddId(_source, GuidA, "Shared");
                PrefabUtility.SaveAsPrefabAsset(_source, _path);
                Object.DestroyImmediate(_source);
            }

            GameObject _asset = AssetDatabase.LoadAssetAtPath<GameObject>(_path);
            GameObject _first = (GameObject)PrefabUtility.InstantiatePrefab(_asset, _scene);
            GameObject _second = (GameObject)PrefabUtility.InstantiatePrefab(_asset, _scene);
            Assert.AreEqual(1, AssetIdCount(_path), "Suspend must keep the stripper away.");

            LogAssert.Expect(LogType.Warning, new Regex("Shared\\.prefab"));
            Assert.IsTrue(PrefabIdStripper.Strip(_path));
            yield return Settle();

            Assert.AreEqual(0, AssetIdCount(_path));
            Assert.AreEqual(1, CountIds(_first) + CountIds(_second), "Only one instance keeps the shared guid.");
            Assert.AreEqual(GuidA, _first.GetComponent<SceneId>().Guid);
        }

        [UnityTest]
        public IEnumerator IdOwnedByNestedPrefab_IsLeftToThatPrefab()
        {
            Scene _scene = util.NewScene("NestedPrefab");
            string _innerPath = PrefabPath("Inner");
            string _outerPath = PrefabPath("Outer");
            using (PrefabIdStripper.Suspend())
            {
                GameObject _inner = SceneIndexTestUtil.NewObject(_scene, "Inner");
                SceneIndexTestUtil.AddId(_inner, GuidA, "Inner");
                PrefabUtility.SaveAsPrefabAsset(_inner, _innerPath);
                Object.DestroyImmediate(_inner);

                GameObject _outer = SceneIndexTestUtil.NewObject(_scene, "Outer");
                GameObject _nested = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(_innerPath), _scene);
                _nested.transform.SetParent(_outer.transform, false);
                PrefabUtility.SaveAsPrefabAsset(_outer, _outerPath);
                Object.DestroyImmediate(_outer);
            }

            Assert.IsFalse(PrefabIdStripper.Strip(_outerPath), "The outer prefab does not own the nested SceneId.");
            Assert.AreEqual(1, AssetIdCount(_innerPath));

            Assert.IsTrue(PrefabIdStripper.Strip(_innerPath));
            yield return Settle();

            Assert.AreEqual(0, AssetIdCount(_innerPath));
            Assert.AreEqual(0, AssetIdCount(_outerPath));
        }

        [UnityTest]
        public IEnumerator Strip_RemovesPathFromPrefabsWithIds()
        {
            Scene _scene = util.NewScene("IndexNotify");
            string _path = PrefabPath("Listed");
            using (PrefabIdStripper.Suspend())
            {
                GameObject _source = SceneIndexTestUtil.NewObject(_scene, "Listed");
                SceneIndexTestUtil.AddId(_source, GuidA, "Listed");
                PrefabUtility.SaveAsPrefabAsset(_source, _path);
                Object.DestroyImmediate(_source);
            }

            util.Index.SetPrefabsWithIds(new System.Collections.Generic.List<string> { _path, "Assets/Other.prefab" });

            Assert.IsTrue(PrefabIdStripper.Strip(_path));
            yield return Settle();

            CollectionAssert.AreEqual(new[] { "Assets/Other.prefab" }, util.Index.PrefabsWithIds);
        }

        [Test]
        public void Strip_PrefabWithoutIds_IsNotLoaded()
        {
            Scene _scene = util.NewScene("NotLoaded");
            string _path = PrefabPath("Plain");
            GameObject _source = SceneIndexTestUtil.NewObject(_scene, "Plain");
            PrefabUtility.SaveAsPrefabAsset(_source, _path);
            Object.DestroyImmediate(_source);
            EditorUtility.UnloadUnusedAssetsImmediate();
            Assume.That(AssetDatabase.IsMainAssetAtPathLoaded(_path), Is.False, "The prefab must start unloaded.");

            Assert.IsFalse(PrefabIdStripper.Strip(_path));

            Assert.IsFalse(AssetDatabase.IsMainAssetAtPathLoaded(_path), "A prefab whose file never mentions SceneId must not be loaded.");
        }

        // ==================== HELPERS ====================

        private static string PrefabPath(string _name) => $"{SceneIndexTestUtil.Folder}/{_name}.prefab";

        private static int AssetIdCount(string _path) =>
            AssetDatabase.LoadAssetAtPath<GameObject>(_path).GetComponentsInChildren<SceneId>(true).Length;

        private static int CountIds(GameObject _go) => _go.GetComponentsInChildren<SceneId>(true).Length;

        private static IEnumerator Settle()
        {
            for (int _i = 0; _i < FramesToSettle; _i++) yield return null;
        }
    }
}
