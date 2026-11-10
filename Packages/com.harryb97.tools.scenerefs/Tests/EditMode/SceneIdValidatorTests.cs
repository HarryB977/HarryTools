using System.Collections.Generic;
using System.Text.RegularExpressions;
using HarryTools.SceneRefs.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HarryTools.SceneRefs.Tests
{
    public class SceneIdValidatorTests
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

        private static SceneIdIndexEntry Entry(string _guid, string _id, string _scene) => new SceneIdIndexEntry
        {
            Guid = _guid,
            Id = _id,
            SceneGuid = _scene,
            SceneName = _scene,
            HierarchyPath = "Root/" + _id,
        };

        // ==================== PURE VALIDATION ====================

        [Test]
        public void Validate_DuplicateGuid_AcrossScenes()
        {
            List<SceneIdConflict> _conflicts = SceneIdValidator.Validate(
                new[] { Entry(GuidA, "One", "sceneA"), Entry(GuidA, "Two", "sceneB") }, new string[0]);

            Assert.AreEqual(1, _conflicts.Count);
            Assert.AreEqual(SceneIdConflictKind.DuplicateGuid, _conflicts[0].Kind);
            Assert.AreEqual(GuidA, _conflicts[0].Key);
            Assert.AreEqual(2, _conflicts[0].Entries.Count);
            StringAssert.Contains("sceneA", _conflicts[0].ToReport());
            StringAssert.Contains("sceneB", _conflicts[0].ToReport());
        }

        [Test]
        public void Validate_DuplicateId_CaseInsensitive()
        {
            List<SceneIdConflict> _conflicts = SceneIdValidator.Validate(
                new[] { Entry(GuidA, "Manzana01", "sceneA"), Entry(GuidB, "manzana01", "sceneA") }, new string[0]);

            Assert.AreEqual(1, _conflicts.Count);
            Assert.AreEqual(SceneIdConflictKind.DuplicateId, _conflicts[0].Kind);
            Assert.AreEqual(2, _conflicts[0].Entries.Count);
        }

        [Test]
        public void Validate_PrefabWithId_IsConflict()
        {
            List<SceneIdConflict> _conflicts = SceneIdValidator.Validate(new SceneIdIndexEntry[0], new[] { "Assets/Thing.prefab" });

            Assert.AreEqual(1, _conflicts.Count);
            Assert.AreEqual(SceneIdConflictKind.IdInPrefabAsset, _conflicts[0].Kind);
            Assert.AreEqual("Assets/Thing.prefab", _conflicts[0].PrefabPath);
            StringAssert.Contains("Assets/Thing.prefab", _conflicts[0].ToReport());
        }

        [Test]
        public void Validate_NoConflicts_Empty()
        {
            List<SceneIdConflict> _conflicts = SceneIdValidator.Validate(
                new[] { Entry(GuidA, "One", "sceneA"), Entry(GuidB, "Two", "sceneB") }, new string[0]);

            Assert.IsEmpty(_conflicts);
        }

        // ==================== PROJECT VALIDATION ====================

        [Test]
        public void Validate_UsesUnsavedLoadedScenes()
        {
            Scene _a = util.NewScene("ValidA");
            SceneIndexTestUtil.AddId(SceneIndexTestUtil.NewObject(_a, "One"), GuidA, "Dup");
            SceneIndexTestUtil.Save(_a);
            // Scene A is only known through the index once closed.
            util.Close(_a);

            Scene _b = util.NewScene("ValidB");
            SceneId _other = SceneIndexTestUtil.AddId(SceneIndexTestUtil.NewObject(_b, "Two"), GuidB, "Other");
            SceneIndexTestUtil.Save(_b);
            Assert.IsEmpty(SceneIdValidator.ValidateProject());

            // Changed in memory only, never saved.
            _other.Set(GuidB, "dup");

            List<SceneIdConflict> _conflicts = SceneIdValidator.ValidateProject();
            Assert.AreEqual(1, _conflicts.Count);
            Assert.AreEqual(SceneIdConflictKind.DuplicateId, _conflicts[0].Kind);

            // The same decision the Play guard takes.
            Assert.IsTrue(PlayModeGuard.ShouldBlock(out List<SceneIdConflict> _blocking));
            Assert.AreEqual(1, _blocking.Count);
        }

        [Test]
        public void PlayModeGuard_DoesNotBlock_WithoutConflicts()
        {
            Scene _a = util.NewScene("PlayOk");
            SceneIndexTestUtil.AddId(SceneIndexTestUtil.NewObject(_a, "One"), GuidA, "Fine");

            Assert.IsFalse(PlayModeGuard.ShouldBlock(out List<SceneIdConflict> _conflicts));
            Assert.IsEmpty(_conflicts);
        }

        [Test]
        public void BuildGuard_Throws_OnConflict()
        {
            Scene _a = util.NewScene("BuildA");
            SceneIndexTestUtil.AddId(SceneIndexTestUtil.NewObject(_a, "One"), GuidA, "Same");
            SceneIndexTestUtil.AddId(SceneIndexTestUtil.NewObject(_a, "Two"), GuidB, "same");
            LogAssert.Expect(LogType.Error, new Regex("^Duplicate scene id 'Same' used by:"));

            BuildFailedException _exception = Assert.Throws<BuildFailedException>(() => new BuildGuard().OnPreprocessBuild(null));
            StringAssert.StartsWith("Build blocked: 1 scene id conflict(s)", _exception.Message);
        }

        [Test]
        public void BuildGuard_Passes_WithoutConflicts()
        {
            Scene _a = util.NewScene("BuildOk");
            SceneIndexTestUtil.AddId(SceneIndexTestUtil.NewObject(_a, "One"), GuidA, "One");

            Assert.DoesNotThrow(() => new BuildGuard().OnPreprocessBuild(null));
        }
    }
}
