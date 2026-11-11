using System.Collections.Generic;
using HarryTools.SceneRefs.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HarryTools.SceneRefs.Tests
{
    public class SceneYamlReaderTests
    {
        // ==================== VARIABLES ====================

        private const string ChildGuid = "0123456789abcdef0123456789abcdef";
        private const string OtherGuid = "abcdefabcdefabcdefabcdefabcdef01";

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
        public void MayContainSceneId_TextPreCheck()
        {
            Scene _with = util.NewScene("PreCheckWith");
            SceneIndexTestUtil.AddId(SceneIndexTestUtil.NewObject(_with, "Lamp"), ChildGuid, "Lamp");
            SceneIndexTestUtil.Save(_with);
            Scene _without = util.NewScene("PreCheckWithout");
            SceneIndexTestUtil.NewObject(_without, "Plain").AddComponent<Light>();
            SceneIndexTestUtil.Save(_without);
            string _binary = SceneIndexTestUtil.Folder + "/Binary.bytes";
            System.IO.File.WriteAllBytes(_binary, new byte[] { 0, 1, 2, 3 });

            Assert.IsTrue(SceneYamlReader.MayContainSceneId(SceneIndexTestUtil.ScenePath("PreCheckWith")));
            Assert.IsFalse(SceneYamlReader.MayContainSceneId(SceneIndexTestUtil.ScenePath("PreCheckWithout")));
            Assert.IsTrue(SceneYamlReader.MayContainSceneId(_binary), "Files not saved as text fall back to the full path.");
            Assert.IsFalse(SceneYamlReader.MayContainSceneId(SceneIndexTestUtil.Folder + "/Missing.unity"));
            Assert.AreEqual(0, SceneYamlReader.FromFile(SceneIndexTestUtil.ScenePath("PreCheckWithout")).Count);
        }

        [Test]
        public void FromFile_ReadsGuidIdNameAndPath_TypesNull()
        {
            Scene _scene = util.NewScene("Yaml");
            GameObject _root = SceneIndexTestUtil.NewObject(_scene, "Root");
            GameObject _child = SceneIndexTestUtil.NewObject(_scene, "Child", _root);
            _child.AddComponent<Light>();
            SceneIndexTestUtil.AddId(_child, ChildGuid, "Lamp");
            SceneIndexTestUtil.AddId(SceneIndexTestUtil.NewObject(_scene, "Other: 'quoted'"), OtherGuid, "Other");
            SceneIndexTestUtil.Save(_scene);
            List<SceneIdIndexEntry> _exact = SceneIndexer.FromLoadedScene(_scene);

            List<SceneIdIndexEntry> _light = SceneYamlReader.FromFile(SceneIndexTestUtil.ScenePath("Yaml"));

            Assert.AreEqual(2, _light.Count);
            _exact.Sort((_a, _b) => string.CompareOrdinal(_a.Guid, _b.Guid));
            _light.Sort((_a, _b) => string.CompareOrdinal(_a.Guid, _b.Guid));
            for (int _i = 0; _i < _exact.Count; _i++)
            {
                Assert.AreEqual(_exact[_i].Guid, _light[_i].Guid);
                Assert.AreEqual(_exact[_i].Id, _light[_i].Id);
                Assert.AreEqual(_exact[_i].SceneGuid, _light[_i].SceneGuid);
                Assert.AreEqual(_exact[_i].SceneName, _light[_i].SceneName);
                Assert.AreEqual(_exact[_i].HierarchyPath, _light[_i].HierarchyPath);
                Assert.IsNull(_light[_i].ComponentTypes);
            }
        }

        [Test]
        public void FromFile_IgnoresOtherMonoBehaviours()
        {
            // Any runtime MonoBehaviour that is not SceneId works; EventSystem comes with uGUI. Looked up by name to keep the asmdef free of a uGUI reference.
            System.Type _other = System.Type.GetType("UnityEngine.EventSystems.EventSystem, UnityEngine.UI");
            if (_other == null) Assert.Ignore("uGUI is not installed.");

            Scene _scene = util.NewScene("Mixed");
            GameObject _go = SceneIndexTestUtil.NewObject(_scene, "Mixed");
            Assert.IsNotNull(_go.AddComponent(_other));
            Assert.IsNotNull(SceneIndexTestUtil.NewObject(_scene, "OnlyOther").AddComponent(_other));
            SceneIndexTestUtil.AddId(_go, ChildGuid, "Mixed");
            SceneIndexTestUtil.Save(_scene);

            string _text = System.IO.File.ReadAllText(SceneIndexTestUtil.ScenePath("Mixed"));
            Assert.AreEqual(3, System.Text.RegularExpressions.Regex.Matches(_text, "MonoBehaviour:").Count, "Two other MonoBehaviours and the SceneId are saved.");

            List<SceneIdIndexEntry> _light = SceneYamlReader.FromFile(SceneIndexTestUtil.ScenePath("Mixed"));

            Assert.AreEqual(1, _light.Count);
            Assert.AreEqual(ChildGuid, _light[0].Guid);
            Assert.AreEqual("Mixed", _light[0].Id);
            Assert.AreEqual("Mixed", _light[0].HierarchyPath);
        }

        [Test]
        public void FromFile_MissingFile_ReturnsEmpty()
        {
            Assert.AreEqual(0, SceneYamlReader.FromFile(SceneIndexTestUtil.ScenePath("DoesNotExist")).Count);
        }
    }
}
