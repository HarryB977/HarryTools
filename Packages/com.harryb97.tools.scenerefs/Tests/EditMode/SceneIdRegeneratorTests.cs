using HarryTools.SceneRefs.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HarryTools.SceneRefs.Tests
{
    public class SceneIdRegeneratorTests
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

        // ==================== TESTS ====================

        [Test]
        public void Regenerate_DuplicatedScene_NewGuidsIdsAndInternalRefs()
        {
            Scene _source = util.NewScene("RegenSource");
            GameObject _target = SceneIndexTestUtil.NewObject(_source, "Apple01");
            SceneIndexTestUtil.AddId(_target, GuidA, "Apple01");
            SceneRefHolder _holder = SceneIndexTestUtil.NewObject(_source, "Holder").AddComponent<SceneRefHolder>();
            _holder.target.SetForTests(_target, null, null, 0, null);
            SceneIndexTestUtil.Save(_source);
            Assert.AreEqual(GuidA, _holder.target.guid);
            util.Close(_source);

            string _copyPath = SceneIndexTestUtil.ScenePath("RegenCopy");
            Assert.IsTrue(AssetDatabase.CopyAsset(SceneIndexTestUtil.ScenePath("RegenSource"), _copyPath));
            SceneIdIndexMaintainer.FlushPending();
            Assert.IsNotEmpty(SceneIdValidator.ValidateProject());

            SceneIdRegenerator.RegenerateScene(_copyPath);
            SceneIdIndexMaintainer.FlushPending();

            Assert.IsEmpty(SceneIdValidator.ValidateProject());
            Assert.IsFalse(SceneManager.GetSceneByPath(_copyPath).isLoaded, "A closed scene is closed again after regenerating.");

            Scene _copy = util.Open(_copyPath);
            GameObject _copyTarget = null;
            SceneRefHolder _copyHolder = null;
            foreach (GameObject _root in _copy.GetRootGameObjects())
            {
                if (_root.name == "Apple01") _copyTarget = _root;
                if (_root.TryGetComponent(out SceneRefHolder _found)) _copyHolder = _found;
            }

            SceneId _copyId = _copyTarget.GetComponent<SceneId>();
            Assert.AreNotEqual(GuidA, _copyId.Guid);
            Assert.AreEqual("Apple02", _copyId.Id);
            Assert.AreEqual(_copyTarget, _copyHolder.target.direct);
            Assert.AreEqual(_copyId.Guid, _copyHolder.target.guid);
            Assert.AreEqual("Apple02 in RegenCopy", _copyHolder.target.label);
        }
    }
}
