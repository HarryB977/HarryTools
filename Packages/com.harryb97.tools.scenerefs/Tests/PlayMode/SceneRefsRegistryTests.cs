using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HarryTools.SceneRefs.Tests
{
    public class SceneRefsRegistryTests
    {
        // ==================== VARIABLES ====================

        private static readonly Regex SceneRefsRegex = new Regex("SceneRefs");

        private readonly List<Scene> scenes = new List<Scene>();

        // ==================== SETUP ====================

        [SetUp]
        public void SetUp() => SceneRefs.Clear();

        [TearDown]
        public void TearDown() => SceneRefsTestUtil.UnloadAll(scenes);

        // ==================== TESTS ====================

        [Test]
        public void InactiveUnderInactiveParent_IsRegistered()
        {
            Scene _scene = SceneRefsTestUtil.NewScene("RegA", scenes);
            GameObject _parent = SceneRefsTestUtil.NewObject(_scene, "Parent", "g1", "Parent", null, false);
            GameObject _child = SceneRefsTestUtil.NewObject(_scene, "Child", "g2", "Child", _parent, false);
            SceneRefs.ScanScene(_scene);

            Assert.AreEqual(_child, SceneRefs.Find("Child"));
            Assert.IsTrue(SceneRefs.TryGetByGuid("g1", out SceneId _id));
            Assert.AreEqual(_parent, _id.gameObject);
        }

        [Test]
        public void Find_IgnoresCase()
        {
            Scene _scene = SceneRefsTestUtil.NewScene("RegB", scenes);
            GameObject _go = SceneRefsTestUtil.NewObject(_scene, "Apple", "g1", "Manzana01");
            SceneRefs.ScanScene(_scene);

            Assert.AreEqual(_go, SceneRefs.Find("manzana01"));
            Assert.IsTrue(SceneRefs.TryFind("MANZANA01", out Transform _t));
            Assert.AreEqual(_go.transform, _t);
        }

        [Test]
        public void Assign_NewId_RegistersAndFinds()
        {
            Scene _scene = SceneRefsTestUtil.NewScene("RegC", scenes);
            GameObject _go = new GameObject("Runtime");
            SceneManager.MoveGameObjectToScene(_go, _scene);

            Assert.IsTrue(SceneRefs.Assign(_go, "Spawned01"));
            Assert.AreEqual(_go, SceneRefs.Find("Spawned01"));
            Assert.IsNotNull(_go.GetComponent<SceneId>());
            Assert.IsTrue(SceneRefs.TryGetByGuid(_go.GetComponent<SceneId>().Guid, out _));
        }

        [Test]
        public void Assign_TakenId_LogsErrorReturnsFalse()
        {
            Scene _scene = SceneRefsTestUtil.NewScene("RegD", scenes);
            GameObject _first = SceneRefsTestUtil.NewObject(_scene, "A", "g1", "Taken");
            SceneRefs.ScanScene(_scene);
            GameObject _other = new GameObject("B");
            SceneManager.MoveGameObjectToScene(_other, _scene);

            LogAssert.Expect(LogType.Error, SceneRefsRegex);
            Assert.IsFalse(SceneRefs.Assign(_other, "taken"));
            Assert.IsNull(_other.GetComponent<SceneId>());
            Assert.AreEqual(_first, SceneRefs.Find("Taken"));
        }

        [UnityTest]
        public IEnumerator SceneUnloaded_Unregisters()
        {
            Scene _scene = SceneRefsTestUtil.NewScene("RegE", scenes);
            SceneRefsTestUtil.NewObject(_scene, "A", "g1", "Gone");
            SceneRefs.ScanScene(_scene);
            Assert.IsNotNull(SceneRefs.Find("Gone"));

            yield return SceneManager.UnloadSceneAsync(_scene);
            scenes.Clear();

            Assert.IsNull(SceneRefs.Find("Gone"));
            Assert.IsFalse(SceneRefs.TryGetByGuid("g1", out _));
        }

        [UnityTest]
        public IEnumerator Destroyed_Unregisters_AndRefReturnsNull()
        {
            Scene _scene = SceneRefsTestUtil.NewScene("RegF", scenes);
            GameObject _go = SceneRefsTestUtil.NewObject(_scene, "A", "g1", "Doomed");
            SceneRefs.ScanScene(_scene);
            SceneRef<Transform> _ref = new SceneRef<Transform>();
            _ref.SetForTests(null, "g1", "UnityEngine.Transform, UnityEngine.CoreModule", 0, "Doomed in RegF");
            Assert.IsNotNull(_ref.Value);

            Object.Destroy(_go);
            yield return null;

            Assert.IsFalse(SceneRefs.TryGetByGuid("g1", out _));
            Assert.IsNull(SceneRefs.Find("Doomed"));
            LogAssert.Expect(LogType.Warning, SceneRefsRegex);
            Assert.IsNull(_ref.Value);
        }

        [Test]
        public void SameSceneLoadedTwice_FirstWins_ErrorPerGuid()
        {
            Scene _a = SceneRefsTestUtil.NewScene("RegG1", scenes);
            Scene _b = SceneRefsTestUtil.NewScene("RegG2", scenes);
            GameObject _first = SceneRefsTestUtil.NewObject(_a, "A", "dup1", "One");
            SceneRefsTestUtil.NewObject(_b, "A", "dup1", "One");
            SceneRefsTestUtil.NewObject(_b, "B", "dup2", "Two");
            SceneRefs.ScanScene(_a);

            LogAssert.Expect(LogType.Error, SceneRefsRegex);
            SceneRefs.ScanScene(_b);

            Assert.IsTrue(SceneRefs.TryGetByGuid("dup1", out SceneId _winner));
            Assert.AreEqual(_first, _winner.gameObject);
            Assert.IsTrue(SceneRefs.TryGetByGuid("dup2", out _));
        }
    }
}
