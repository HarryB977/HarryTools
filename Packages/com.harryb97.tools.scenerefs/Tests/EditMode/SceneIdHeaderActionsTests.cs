using HarryTools.SceneRefs.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HarryTools.SceneRefs.Tests
{
    public class SceneIdHeaderActionsTests
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

        // ==================== TESTS ====================

        [Test]
        public void Add_UsesSanitizedUniqueName()
        {
            Scene _scene = util.NewScene("HeaderAdd");
            GameObject _first = SceneIndexTestUtil.NewObject(_scene, "Lamp Post");
            GameObject _second = SceneIndexTestUtil.NewObject(_scene, "Lamp Post");

            SceneIdHeaderActions.Add(new[] { _first, _second });

            Assert.AreEqual("Lamp_Post", _first.GetComponent<SceneId>().Id);
            Assert.AreEqual("Lamp_Post1", _second.GetComponent<SceneId>().Id);
            Assert.IsFalse(string.IsNullOrEmpty(_second.GetComponent<SceneId>().Guid));
        }

        [Test]
        public void TrySetId_Conflict_SavesAndReportsOther()
        {
            Scene _scene = util.NewScene("HeaderConflict");
            GameObject _other = SceneIndexTestUtil.NewObject(_scene, "Parent");
            GameObject _child = SceneIndexTestUtil.NewObject(_scene, "Apple", _other);
            SceneIndexTestUtil.AddId(_child, GuidA, "Apple01");
            GameObject _target = SceneIndexTestUtil.NewObject(_scene, "Target");
            SceneIndexTestUtil.AddId(_target, GuidB, "Target");

            bool _ok = SceneIdHeaderActions.TrySetId(_target, "apple01", out string _conflict);

            Assert.IsFalse(_ok);
            Assert.AreEqual("HeaderConflict/Parent/Apple", _conflict);
            Assert.AreEqual("apple01", _target.GetComponent<SceneId>().Id);
            Assert.AreEqual(GuidB, _target.GetComponent<SceneId>().Guid);
        }

        [Test]
        public void TrySetId_Free_ReturnsTrue()
        {
            Scene _scene = util.NewScene("HeaderFree");
            GameObject _target = SceneIndexTestUtil.NewObject(_scene, "Target");
            SceneIndexTestUtil.AddId(_target, GuidB, "Target");

            Assert.IsTrue(SceneIdHeaderActions.TrySetId(_target, "Other", out string _conflict));
            Assert.IsNull(_conflict);
            Assert.AreEqual("Other", _target.GetComponent<SceneId>().Id);
        }

        [Test]
        public void RegenerateGuid_KeepsVisibleId()
        {
            Scene _scene = util.NewScene("HeaderRegen");
            GameObject _target = SceneIndexTestUtil.NewObject(_scene, "Target");
            SceneIndexTestUtil.AddId(_target, GuidB, "Keep_Me");

            SceneIdHeaderActions.RegenerateGuid(_target);

            SceneId _id = _target.GetComponent<SceneId>();
            Assert.AreEqual("Keep_Me", _id.Id);
            Assert.AreNotEqual(GuidB, _id.Guid);
            Assert.IsFalse(string.IsNullOrEmpty(_id.Guid));
        }

        [Test]
        public void Remove_IsUndoable()
        {
            Scene _scene = util.NewScene("HeaderRemove");
            GameObject _target = SceneIndexTestUtil.NewObject(_scene, "Target");
            SceneIdHeaderActions.Add(new[] { _target });
            Undo.IncrementCurrentGroup();
            string _guid = _target.GetComponent<SceneId>().Guid;

            SceneIdHeaderActions.Remove(_target);
            Assert.IsNull(_target.GetComponent<SceneId>());

            Undo.PerformUndo();
            SceneId _restored = _target.GetComponent<SceneId>();
            Assert.IsNotNull(_restored);
            Assert.AreEqual(_guid, _restored.Guid);
        }
    }
}
