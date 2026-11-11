using System.Collections.Generic;
using HarryTools.SceneRefs.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HarryTools.SceneRefs.Tests
{
    public class SceneRefAssignTests
    {
        // ==================== VARIABLES ====================

        private const string GuidA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string GuidB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

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

        private static SerializedProperty Property(Object _owner, string _field) => new SerializedObject(_owner).FindProperty(_field);

        /// <summary>
        /// A holder saved as a real .asset and loaded back. Unity only refuses scene objects in persistent owners,
        /// so an in-memory instance would hide that case.
        /// </summary>
        private static SceneRefAssetHolder NewAsset(string _name)
        {
            string _path = $"{SceneIndexTestUtil.Folder}/{_name}.asset";
            SceneRefAssetHolder _created = ScriptableObject.CreateInstance<SceneRefAssetHolder>();
            AssetDatabase.CreateAsset(_created, _path);
            AssetDatabase.SaveAssetIfDirty(_created);

            SceneRefAssetHolder _asset = AssetDatabase.LoadAssetAtPath<SceneRefAssetHolder>(_path);
            Assert.IsNotNull(_asset);
            Assert.IsTrue(EditorUtility.IsPersistent(_asset));
            return _asset;
        }

        // ==================== TRY ASSIGN ====================

        [Test]
        public void TryAssign_SameScene_DirectAndGuid()
        {
            Scene _scene = util.NewScene("AssignSame");
            GameObject _target = SceneIndexTestUtil.NewObject(_scene, "Lamp");
            SceneIndexTestUtil.AddId(_target, GuidA, "Lamp");
            SceneRefHolder _holder = SceneIndexTestUtil.NewObject(_scene, "Holder").AddComponent<SceneRefHolder>();

            Assert.IsTrue(SceneRefAssign.TryAssign(Property(_holder, "target"), _target, typeof(GameObject)));

            Assert.AreEqual(_target, _holder.target.direct);
            Assert.AreEqual(GuidA, _holder.target.guid);
            Assert.AreEqual(string.Empty, _holder.target.componentType);
            Assert.AreEqual("Lamp in AssignSame", _holder.target.label);
        }

        [Test]
        public void TryAssign_OtherScene_GuidOnly()
        {
            Scene _a = util.NewScene("AssignA");
            Scene _b = util.NewScene("AssignB");
            GameObject _target = SceneIndexTestUtil.NewObject(_b, "Target");
            SceneIndexTestUtil.AddId(_target, GuidA, "Target");
            SceneRefHolder _holder = SceneIndexTestUtil.NewObject(_a, "Holder").AddComponent<SceneRefHolder>();

            Assert.IsTrue(SceneRefAssign.TryAssign(Property(_holder, "target"), _target, typeof(GameObject)));

            Assert.IsTrue(_holder.target.direct == null);
            Assert.AreEqual(GuidA, _holder.target.guid);
            Assert.AreEqual("Target in AssignB", _holder.target.label);
        }

        [Test]
        public void TryAssign_ToScriptableObjectAsset_GuidOnly()
        {
            Scene _scene = util.NewScene("AssignAsset");
            GameObject _target = SceneIndexTestUtil.NewObject(_scene, "Target");
            SceneIndexTestUtil.AddId(_target, GuidA, "Target");
            SceneRefAssetHolder _holder = NewAsset("AssignAssetHolder");

            Assert.IsTrue(SceneRefAssign.TryAssign(Property(_holder, "target"), _target, typeof(GameObject)));

            Assert.IsTrue(_holder.target.direct == null);
            Assert.AreEqual(GuidA, _holder.target.guid);
            Assert.AreEqual("Target in AssignAsset", _holder.target.label);
        }

        [Test]
        public void TryAssign_ToScriptableObjectAsset_AddsSceneIdIfMissing()
        {
            Scene _scene = util.NewScene("AssignAssetNewId");
            GameObject _target = SceneIndexTestUtil.NewObject(_scene, "Apple");
            SceneRefAssetHolder _holder = NewAsset("AssignAssetNewIdHolder");

            Assert.IsTrue(SceneRefAssign.TryAssign(Property(_holder, "target"), _target, typeof(GameObject)));

            SceneId _id = _target.GetComponent<SceneId>();
            Assert.IsNotNull(_id);
            Assert.AreEqual(_id.Guid, _holder.target.guid);
            Assert.IsTrue(_holder.target.direct == null);
            Assert.AreEqual("Apple in AssignAssetNewId", _holder.target.label);
        }

        [Test]
        public void AssignFromIndex_LoadedObject_IntoScriptableObjectAsset()
        {
            Scene _scene = util.NewScene("PickAsset");
            GameObject _target = SceneIndexTestUtil.NewObject(_scene, "Apple");
            SceneIndexTestUtil.AddId(_target, GuidA, "Apple");
            SceneRefAssetHolder _holder = NewAsset("PickAssetHolder");
            SceneIdIndexEntry _entry = new SceneIdIndexEntry
            {
                Guid = GuidA, Id = "Apple", SceneGuid = AssetDatabase.AssetPathToGUID(_scene.path), SceneName = "PickAsset", HierarchyPath = "Apple",
                ComponentTypes = new string[0],
            };

            SceneRefAssign.AssignFromIndex(Property(_holder, "target"), _entry, 0, typeof(GameObject));

            Assert.IsTrue(_holder.target.direct == null);
            Assert.AreEqual(GuidA, _holder.target.guid);
            Assert.AreEqual("Apple in PickAsset", _holder.target.label);
        }

        [Test]
        public void TryAssign_SerializeReferenceListInScriptableObjectAsset_GuidOnly()
        {
            Scene _scene = util.NewScene("AssignAssetList");
            GameObject _target = SceneIndexTestUtil.NewObject(_scene, "Apple");
            SceneIndexTestUtil.AddId(_target, GuidA, "Apple");
            SceneRefAssetHolder _holder = NewAsset("AssignAssetListHolder");
            _holder.items.Add(new SceneRefListItem());
            EditorUtility.SetDirty(_holder);
            AssetDatabase.SaveAssetIfDirty(_holder);

            Assert.IsTrue(SceneRefAssign.TryAssign(Property(_holder, "items.Array.data[0].target"), _target, typeof(GameObject)));

            SceneRefListItem _item = (SceneRefListItem)_holder.items[0];
            Assert.IsTrue(_item.target.direct == null);
            Assert.AreEqual(GuidA, _item.target.guid);
            Assert.AreEqual("Apple in AssignAssetList", _item.target.label);
        }

        [Test]
        public void TryAssign_GameObjectIntoLightField_TakesLight()
        {
            Scene _scene = util.NewScene("AssignLight");
            GameObject _target = SceneIndexTestUtil.NewObject(_scene, "Lamp");
            Light _light = _target.AddComponent<Light>();
            SceneRefHolder _holder = SceneIndexTestUtil.NewObject(_scene, "Holder").AddComponent<SceneRefHolder>();

            Assert.IsTrue(SceneRefAssign.TryAssign(Property(_holder, "light"), _target, typeof(Light)));

            Assert.AreEqual(_light, _holder.light.direct);
            Assert.AreEqual(TypeName<Light>(), _holder.light.componentType);
            Assert.AreEqual(0, _holder.light.componentIndex);
            Assert.AreEqual(_target.GetComponent<SceneId>().Guid, _holder.light.guid);
        }

        [Test]
        public void TryAssign_GameObjectWithoutT_Rejected()
        {
            Scene _scene = util.NewScene("AssignNoLight");
            GameObject _target = SceneIndexTestUtil.NewObject(_scene, "Empty");
            SceneRefHolder _holder = SceneIndexTestUtil.NewObject(_scene, "Holder").AddComponent<SceneRefHolder>();

            Assert.IsFalse(SceneRefAssign.TryAssign(Property(_holder, "light"), _target, typeof(Light)));

            Assert.IsFalse(_holder.light.IsAssigned);
            Assert.IsNull(_target.GetComponent<SceneId>());
        }

        [Test]
        public void TryAssign_Asset_Rejected()
        {
            Scene _scene = util.NewScene("AssignPrefabAsset");
            GameObject _source = SceneIndexTestUtil.NewObject(_scene, "Thing");
            GameObject _asset = PrefabUtility.SaveAsPrefabAsset(_source, SceneIndexTestUtil.Folder + "/Thing.prefab");
            Object.DestroyImmediate(_source);
            SceneRefHolder _holder = SceneIndexTestUtil.NewObject(_scene, "Holder").AddComponent<SceneRefHolder>();
            Material _material = new Material(Shader.Find("Hidden/InternalErrorShader"));
            try
            {
                Assert.IsFalse(SceneRefAssign.TryAssign(Property(_holder, "target"), _asset, typeof(GameObject)));
                Assert.IsFalse(SceneRefAssign.TryAssign(Property(_holder, "target"), _material, typeof(GameObject)));

                Assert.IsFalse(_holder.target.IsAssigned);
                Assert.IsNull(_asset.GetComponent<SceneId>());
            }
            finally
            {
                Object.DestroyImmediate(_material);
            }
        }

        [Test]
        public void TryAssign_InsideSamePrefabAsset_DirectOnly_NoSceneId()
        {
            Scene _scene = util.NewScene("AssignInsidePrefab");
            GameObject _root = SceneIndexTestUtil.NewObject(_scene, "Root");
            SceneIndexTestUtil.NewObject(_scene, "Child", _root);
            _root.AddComponent<SceneRefHolder>();
            string _path = SceneIndexTestUtil.Folder + "/Inside.prefab";
            GameObject _asset = PrefabUtility.SaveAsPrefabAsset(_root, _path);
            Object.DestroyImmediate(_root);

            SceneRefHolder _holder = _asset.GetComponent<SceneRefHolder>();
            GameObject _child = _asset.transform.Find("Child").gameObject;
            Assert.IsTrue(SceneRefAssign.TryAssign(Property(_holder, "target"), _child, typeof(GameObject)));

            Assert.AreEqual(_child, _holder.target.direct);
            Assert.IsTrue(string.IsNullOrEmpty(_holder.target.guid));
            Assert.IsNull(_child.GetComponent<SceneId>());

            // Prefab contents loaded for editing live in a preview scene, like Prefab Mode.
            GameObject _contents = PrefabUtility.LoadPrefabContents(_path);
            try
            {
                SceneRefHolder _stageHolder = _contents.GetComponent<SceneRefHolder>();
                GameObject _stageChild = _contents.transform.Find("Child").gameObject;
                Assert.IsTrue(SceneRefAssign.TryAssign(Property(_stageHolder, "target"), _stageChild, typeof(GameObject)));

                Assert.AreEqual(_stageChild, _stageHolder.target.direct);
                Assert.IsTrue(string.IsNullOrEmpty(_stageHolder.target.guid));
                Assert.IsNull(_stageChild.GetComponent<SceneId>());
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(_contents);
            }
        }

        [Test]
        public void TryAssign_AddsSceneIdIfMissing()
        {
            Scene _scene = util.NewScene("AssignNewId");
            GameObject _target = SceneIndexTestUtil.NewObject(_scene, "Lamp Post");
            SceneRefHolder _holder = SceneIndexTestUtil.NewObject(_scene, "Holder").AddComponent<SceneRefHolder>();

            Assert.IsTrue(SceneRefAssign.TryAssign(Property(_holder, "target"), _target, typeof(GameObject)));

            SceneId _id = _target.GetComponent<SceneId>();
            Assert.IsNotNull(_id);
            Assert.AreEqual("Lamp_Post", _id.Id);
            Assert.AreEqual(_id.Guid, _holder.target.guid);
            Assert.AreEqual("Lamp_Post in AssignNewId", _holder.target.label);
        }

        [Test]
        public void TryAssign_ComponentIntoGameObjectField_TakesGameObject()
        {
            Scene _scene = util.NewScene("AssignComponent");
            GameObject _target = SceneIndexTestUtil.NewObject(_scene, "Lamp");
            Light _light = _target.AddComponent<Light>();
            SceneRefHolder _holder = SceneIndexTestUtil.NewObject(_scene, "Holder").AddComponent<SceneRefHolder>();

            Assert.IsTrue(SceneRefAssign.TryAssign(Property(_holder, "target"), _light, typeof(GameObject)));

            Assert.AreEqual(_target, _holder.target.direct);
        }

        // ==================== INDEX ====================

        [Test]
        public void AssignFromIndex_ClosedScene_GuidAndLabel()
        {
            Scene _scene = util.NewScene("AssignFromIndex");
            SceneRefHolder _holder = SceneIndexTestUtil.NewObject(_scene, "Holder").AddComponent<SceneRefHolder>();
            SceneIdIndexEntry _entry = new SceneIdIndexEntry
            {
                Guid = GuidB, Id = "Lamp", SceneGuid = "closedscene", SceneName = "Kitchen", HierarchyPath = "Lamp",
                ComponentTypes = new[] { typeof(Light).FullName },
            };
            util.Index.ReplaceScene("closedscene", new List<SceneIdIndexEntry> { _entry });

            SceneRefAssign.AssignFromIndex(Property(_holder, "light"), _entry, 0, typeof(Light));

            Assert.IsTrue(_holder.light.direct == null);
            Assert.AreEqual(GuidB, _holder.light.guid);
            Assert.AreEqual(TypeName<Light>(), _holder.light.componentType);
            Assert.AreEqual(0, _holder.light.componentIndex);
            Assert.AreEqual("Lamp in Kitchen", _holder.light.label);
        }

        [Test]
        public void ConcreteType_BaseType_ResolvesFromIndexTypes()
        {
            SceneIdIndexEntry _entry = new SceneIdIndexEntry { Guid = GuidB, ComponentTypes = new[] { typeof(BoxCollider).FullName, typeof(Light).FullName } };
            SceneIdIndexEntry _unknown = new SceneIdIndexEntry { Guid = GuidA, ComponentTypes = null };

            Assert.AreEqual(typeof(Light), SceneRefAssign.ConcreteType(_entry, typeof(Behaviour)));
            Assert.AreEqual(typeof(BoxCollider), SceneRefAssign.ConcreteType(_entry, typeof(Collider)));
            Assert.IsNull(SceneRefAssign.ConcreteType(_entry, typeof(AudioSource)));
            Assert.IsTrue(SceneRefAssign.Matches(_entry, typeof(Behaviour)));
            Assert.IsFalse(SceneRefAssign.Matches(_entry, typeof(AudioSource)));
            Assert.IsTrue(SceneRefAssign.Matches(_entry, typeof(GameObject)));
            Assert.IsTrue(SceneRefAssign.Matches(_unknown, typeof(AudioSource)));
            Assert.IsNull(SceneRefAssign.ConcreteType(_unknown, typeof(AudioSource)));
        }

        [Test]
        public void Clear_EmptiesEveryField()
        {
            Scene _scene = util.NewScene("AssignClear");
            GameObject _target = SceneIndexTestUtil.NewObject(_scene, "Lamp");
            SceneRefHolder _holder = SceneIndexTestUtil.NewObject(_scene, "Holder").AddComponent<SceneRefHolder>();
            _holder.target.SetForTests(_target, GuidA, "x", 2, "Lamp in AssignClear");

            SceneRefAssign.Clear(Property(_holder, "target"));

            Assert.IsFalse(_holder.target.IsAssigned);
            Assert.AreEqual(string.Empty, _holder.target.label);
            Assert.AreEqual(0, _holder.target.componentIndex);
        }

        // ==================== DRAWER ====================

        [Test]
        public void TargetTypeOf_FieldArrayAndList()
        {
            Assert.AreEqual(typeof(Light), SceneRefDrawer.TargetTypeOf(typeof(SceneRef<Light>)));
            Assert.AreEqual(typeof(GameObject), SceneRefDrawer.TargetTypeOf(typeof(SceneRef<GameObject>[])));
            Assert.AreEqual(typeof(AudioSource), SceneRefDrawer.TargetTypeOf(typeof(List<SceneRef<AudioSource>>)));
        }

        [Test]
        public void DisplayText_UsesCurrentIndexNameOrMissing()
        {
            util.Index.ReplaceScene("closedscene", new List<SceneIdIndexEntry>
            {
                new SceneIdIndexEntry { Guid = GuidB, Id = "Lamp_Renamed", SceneName = "Kitchen", HierarchyPath = "Lamp" },
            });

            Assert.AreEqual("Lamp_Renamed · Kitchen", SceneRefDrawer.DisplayText(GuidB, "Lamp in Kitchen", out bool _missing));
            Assert.IsFalse(_missing);
            Assert.AreEqual("Gone · missing", SceneRefDrawer.DisplayText(GuidA, "Gone in Old", out _missing));
            Assert.IsTrue(_missing);
            Assert.AreEqual("missing", SceneRefDrawer.DisplayText(GuidA, string.Empty, out _missing));
            Assert.IsTrue(_missing);
        }
    }
}
