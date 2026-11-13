using System.Collections.Generic;
using System.Linq;
using HarryTools.Flags.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HarryTools.Flags.Tests
{
    public class FlagValidatorTests
    {
        // ==================== VARIABLES ====================

        private const string SceneA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string SceneB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        private FlagDatabase database;
        private FlagUsageIndex index;
        private string existingSceneGuid;

        // ==================== SETUP ====================

        [SetUp]
        public void SetUp()
        {
            database = ScriptableObject.CreateInstance<FlagDatabase>();
            index = ScriptableObject.CreateInstance<FlagUsageIndex>();
            existingSceneGuid = UnityEditor.AssetDatabase.AssetPathToGUID("Packages/com.harryb97.tools.flags/package.json");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(database);
            Object.DestroyImmediate(index);
        }

        // ==================== TESTS ====================

        [Test]
        public void Clean_NoIssues()
        {
            Add("g1", "Door", FlagScope.Global, string.Empty);
            Add("l1", "Door", FlagScope.Local, existingSceneGuid);
            Use("g1", string.Empty);

            Assert.That(FlagValidator.Validate(database, index), Is.Empty);
        }

        [Test]
        public void DuplicateGuid_Blocks()
        {
            Add("same", "One", FlagScope.Global, string.Empty);
            Add("same", "Two", FlagScope.Global, string.Empty);

            List<FlagIssue> _issues = FlagValidator.Validate(database, index);

            Assert.That(_issues.Count(_i => _i.Kind == FlagIssueKind.DuplicateGuid && _i.Blocking && _i.FlagGuid == "same"), Is.EqualTo(1));
        }

        [Test]
        public void DuplicateNameSameScope_IgnoresCase_Blocks()
        {
            Add("g1", "Door", FlagScope.Global, string.Empty);
            Add("g2", "DOOR", FlagScope.Global, string.Empty);

            List<FlagIssue> _issues = FlagValidator.Validate(database, index);

            Assert.That(_issues.Count(_i => _i.Kind == FlagIssueKind.DuplicateName && _i.Blocking), Is.EqualTo(1));
        }

        [Test]
        public void SameNameGlobalAndLocal_NoIssue()
        {
            Add("g1", "Door", FlagScope.Global, string.Empty);
            Add("l1", "Door", FlagScope.Local, existingSceneGuid);

            Assert.That(FlagValidator.Validate(database, index), Is.Empty);
        }

        [Test]
        public void SameNameLocalInDifferentScenes_NoIssue()
        {
            Add("l1", "Door", FlagScope.Local, existingSceneGuid);
            Add("l2", "Door", FlagScope.Local, SceneB);

            Assert.That(FlagValidator.Validate(database, index).Any(_i => _i.Kind == FlagIssueKind.DuplicateName), Is.False);
        }

        [Test]
        public void BrokenRef_WarnsNotBlocks()
        {
            Use("deleted", SceneA);

            List<FlagIssue> _issues = FlagValidator.Validate(database, index);

            Assert.That(_issues.Count, Is.EqualTo(1));
            Assert.That(_issues[0].Kind, Is.EqualTo(FlagIssueKind.BrokenRef));
            Assert.That(_issues[0].Blocking, Is.False);
            Assert.That(_issues[0].Usage, Is.Not.Null);
        }

        [Test]
        public void ForeignLocalRef_WarnsNotBlocks()
        {
            Add("l1", "Door", FlagScope.Local, existingSceneGuid);
            Use("l1", SceneB);

            List<FlagIssue> _issues = FlagValidator.Validate(database, index);

            Assert.That(_issues.Count(_i => _i.Kind == FlagIssueKind.ForeignLocalRef && !_i.Blocking), Is.EqualTo(1));
        }

        [Test]
        public void MissingScene_WarnsNotBlocks()
        {
            Add("l1", "Door", FlagScope.Local, SceneA);

            List<FlagIssue> _issues = FlagValidator.Validate(database, index);

            Assert.That(_issues.Count, Is.EqualTo(1));
            Assert.That(_issues[0].Kind, Is.EqualTo(FlagIssueKind.MissingScene));
            Assert.That(_issues[0].Blocking, Is.False);
            Assert.That(_issues[0].FlagGuid, Is.EqualTo("l1"));
        }

        [Test]
        public void IsLoadableAtRuntime_OnlyResourcesFlagDatabase()
        {
            Assert.That(FlagDatabaseLocator.IsLoadableAtRuntime("Assets/HarryTools/Flags/Resources/FlagDatabase.asset"), Is.True);
            Assert.That(FlagDatabaseLocator.IsLoadableAtRuntime("Packages/com.game/Resources/FlagDatabase.asset"), Is.True);
            Assert.That(FlagDatabaseLocator.IsLoadableAtRuntime("Assets/Data/FlagDatabase.asset"), Is.False, "Not in Resources.");
            Assert.That(FlagDatabaseLocator.IsLoadableAtRuntime("Assets/Resources/Flags.asset"), Is.False, "Wrong name.");
            Assert.That(FlagDatabaseLocator.IsLoadableAtRuntime("Assets/Resources/Sub/FlagDatabase.asset"), Is.False, "In a subfolder of Resources.");
            Assert.That(FlagDatabaseLocator.IsLoadableAtRuntime(string.Empty), Is.False);
        }

        [Test]
        public void ContextOf_SceneUse_ObjectWhenLoaded_SceneAssetWhenClosed()
        {
            // The usage maintainer reacts to the temporary scene; it must only see these instances.
            FlagDatabaseLocator.OverrideForTests = database;
            FlagUsageIndexLocator.OverrideForTests = index;
            List<Scene> _scenes = new List<Scene>();
            try
            {
                FlagUsageTestScenes.EnsureFolder();
                string _path = FlagUsageTestScenes.Write("Context");
                FlagUsageEntry _use = new FlagUsageEntry { flagGuid = "g1", sceneGuid = AssetDatabase.AssetPathToGUID(_path), hierarchyPath = "Host" };

                Scene _scene = FlagUsageTestScenes.Open(_path, _scenes);
                Assert.That(FlagValidator.ContextOf(_use), Is.SameAs(FlagUsageTestScenes.Host(_scene)));

                FlagUsageTestScenes.CloseAll(_scenes);
                Assert.That(FlagValidator.ContextOf(_use), Is.SameAs(AssetDatabase.LoadAssetAtPath<SceneAsset>(_path)));
                Assert.That(FlagValidator.ContextOf(_use), Is.Not.Null);
            }
            finally
            {
                FlagUsageTestScenes.CloseAll(_scenes);
                AssetDatabase.DeleteAsset(FlagUsageTestScenes.Folder);
                FlagUsageMaintainer.FlushPending();
                FlagUsageIndexLocator.OverrideForTests = null;
                FlagDatabaseLocator.OverrideForTests = null;
            }
        }

        // ==================== HELPERS ====================

        private void Add(string _guid, string _name, FlagScope _scope, string _sceneGuid)
        {
            database.MutableFlags.Add(new FlagDefinition(_guid, _name, _scope, _sceneGuid, "Scene"));
            database.Invalidate();
        }

        private void Use(string _flagGuid, string _hostSceneGuid)
        {
            index.ReplaceScene(string.IsNullOrEmpty(_hostSceneGuid) ? SceneA : _hostSceneGuid, new List<FlagUsageEntry>
            {
                new FlagUsageEntry { flagGuid = _flagGuid, hierarchyPath = "Host", componentType = "Thing" }
            });
        }
    }
}
