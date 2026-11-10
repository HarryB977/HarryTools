using HarryTools.SceneRefs.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HarryTools.SceneRefs.Tests
{
    public class SceneIdDuplicateFixerTests
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

        /// <summary>Copies a hierarchy the way Ctrl+D does: same scene, internal references remapped to the copy.</summary>
        private static GameObject Copy(Scene _scene, GameObject _source)
        {
            Scene _previous = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(_scene);
            GameObject _copy = Object.Instantiate(_source);
            SceneManager.SetActiveScene(_previous);
            return _copy;
        }

        // ==================== TESTS ====================

        [Test]
        public void Duplicate_GetsNewGuidAndIncrementedId()
        {
            Scene _scene = util.NewScene("DupSimple");
            GameObject _original = SceneIndexTestUtil.NewObject(_scene, "Manzana01");
            SceneIndexTestUtil.AddId(_original, GuidA, "Manzana01");

            GameObject _copy = Copy(_scene, _original);
            SceneIdDuplicateFixer.FixNewHierarchy(_copy);

            SceneId _copyId = _copy.GetComponent<SceneId>();
            Assert.AreNotEqual(GuidA, _copyId.Guid);
            Assert.AreEqual(32, _copyId.Guid.Length);
            Assert.AreEqual("Manzana02", _copyId.Id);
            Assert.AreEqual(GuidA, _original.GetComponent<SceneId>().Guid);
            Assert.AreEqual("Manzana01", _original.GetComponent<SceneId>().Id);
        }

        [Test]
        public void Duplicate_InternalRef_PointsToCopy()
        {
            Scene _scene = util.NewScene("DupInternal");
            GameObject _root = SceneIndexTestUtil.NewObject(_scene, "Root");
            SceneRefHolder _holder = SceneIndexTestUtil.NewObject(_scene, "Holder", _root).AddComponent<SceneRefHolder>();
            GameObject _target = SceneIndexTestUtil.NewObject(_scene, "Target", _root);
            SceneIndexTestUtil.AddId(_target, GuidB, "Target");
            _holder.target.SetForTests(_target, GuidB, string.Empty, 0, "Target in DupInternal");

            GameObject _copy = Copy(_scene, _root);
            SceneIdDuplicateFixer.FixNewHierarchy(_copy);

            SceneRefHolder _copyHolder = _copy.GetComponentInChildren<SceneRefHolder>();
            GameObject _copyTarget = _copy.transform.Find("Target").gameObject;
            SceneId _copyId = _copyTarget.GetComponent<SceneId>();
            Assert.AreNotEqual(GuidB, _copyId.Guid);
            Assert.AreEqual(_copyTarget, _copyHolder.target.direct);
            Assert.AreEqual(_copyId.Guid, _copyHolder.target.guid);
            Assert.AreEqual(_copyId.Id + " in DupInternal", _copyHolder.target.label);

            // The original is left untouched.
            Assert.AreEqual(_target, _holder.target.direct);
            Assert.AreEqual(GuidB, _holder.target.guid);
        }
    }
}
