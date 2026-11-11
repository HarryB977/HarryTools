using HarryTools.Flags.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HarryTools.Flags.Tests
{
    public class FlagDatabaseEditingTests
    {
        // ==================== VARIABLES ====================

        private const string SceneA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string SceneB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        private FlagDatabase database;

        // ==================== SETUP ====================

        [SetUp]
        public void SetUp() => database = ScriptableObject.CreateInstance<FlagDatabase>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(database);

        // ==================== TESTS ====================

        [Test]
        public void Create_Global_AssignsGuidAndDefaults()
        {
            Assert.IsTrue(FlagDatabaseEditing.TryCreate(database, FlagScope.Global, null, "Door01", out FlagDefinition _created, out string _error), _error);

            Assert.IsTrue(System.Text.RegularExpressions.Regex.IsMatch(_created.Guid, "^[0-9a-f]{32}$"));
            Assert.AreEqual("Door01", _created.Name);
            Assert.AreEqual(FlagScope.Global, _created.Scope);
            Assert.IsFalse(_created.InitialValue);
            Assert.AreEqual("Global / Door01", _created.DisplayPath);
            Assert.AreEqual(1, database.Flags.Count);
        }

        [Test]
        public void Create_DuplicateInSameScope_IgnoresCase_Fails()
        {
            Assert.IsTrue(FlagDatabaseEditing.TryCreate(database, FlagScope.Global, null, "Door01", out _, out _));

            Assert.IsFalse(FlagDatabaseEditing.TryCreate(database, FlagScope.Global, null, "door01", out FlagDefinition _created, out string _error));
            Assert.IsNull(_created);
            Assert.IsNotEmpty(_error);
            Assert.AreEqual(1, database.Flags.Count);
        }

        [Test]
        public void Create_SameNameGlobalAndLocal_Allowed()
        {
            Assert.IsTrue(FlagDatabaseEditing.TryCreate(database, FlagScope.Global, null, "Door01", out _, out _));
            Assert.IsTrue(FlagDatabaseEditing.TryCreate(database, FlagScope.Local, SceneA, "Door01", out _, out string _error), _error);
        }

        [Test]
        public void Create_SameNameInTwoScenes_Allowed()
        {
            Assert.IsTrue(FlagDatabaseEditing.TryCreate(database, FlagScope.Local, SceneA, "Door01", out _, out _));
            Assert.IsTrue(FlagDatabaseEditing.TryCreate(database, FlagScope.Local, SceneB, "Door01", out _, out string _error), _error);
            Assert.IsFalse(FlagDatabaseEditing.TryCreate(database, FlagScope.Local, SceneA, "DOOR01", out _, out _));
        }

        [Test]
        public void Create_InvalidName_Fails()
        {
            Assert.IsFalse(FlagDatabaseEditing.TryCreate(database, FlagScope.Global, null, "Door 01", out FlagDefinition _created, out string _error));
            Assert.IsNull(_created);
            Assert.IsNotEmpty(_error);
            Assert.AreEqual(0, database.Flags.Count);
        }

        [Test]
        public void Rename_ToTakenName_Fails()
        {
            FlagDatabaseEditing.TryCreate(database, FlagScope.Global, null, "A", out FlagDefinition _a, out _);
            FlagDatabaseEditing.TryCreate(database, FlagScope.Global, null, "B", out _, out _);

            Assert.IsFalse(FlagDatabaseEditing.TryRename(database, _a.Guid, "b", out string _error));
            Assert.IsNotEmpty(_error);
            Assert.AreEqual("A", _a.Name);
        }

        [Test]
        public void Rename_KeepsGuid()
        {
            FlagDatabaseEditing.TryCreate(database, FlagScope.Global, null, "A", out FlagDefinition _a, out _);
            string _guid = _a.Guid;

            Assert.IsTrue(FlagDatabaseEditing.TryRename(database, _guid, "Renamed", out string _error), _error);

            Assert.IsTrue(database.TryGet(_guid, out FlagDefinition _found));
            Assert.AreEqual("Renamed", _found.Name);
        }

        [Test]
        public void Delete_RemovesEntry()
        {
            FlagDatabaseEditing.TryCreate(database, FlagScope.Global, null, "A", out FlagDefinition _a, out _);

            Assert.IsTrue(FlagDatabaseEditing.Delete(database, _a.Guid));

            Assert.AreEqual(0, database.Flags.Count);
            Assert.IsFalse(database.TryGet(_a.Guid, out _));
            Assert.IsFalse(FlagDatabaseEditing.Delete(database, _a.Guid));
        }

        [Test]
        public void Undo_RevertsCreate()
        {
            FlagDatabaseEditing.TryCreate(database, FlagScope.Global, null, "A", out FlagDefinition _a, out _);
            Assert.AreEqual(1, database.Flags.Count);

            Undo.PerformUndo();

            Assert.AreEqual(0, database.Flags.Count);
            Assert.IsFalse(database.TryGet(_a.Guid, out _));
        }

        [Test]
        public void Entries_AreSorted()
        {
            FlagDatabaseEditing.TryCreate(database, FlagScope.Local, SceneB, "Zeta", out FlagDefinition _bZeta, out _);
            FlagDatabaseEditing.TryCreate(database, FlagScope.Local, SceneA, "Alpha", out FlagDefinition _aAlpha, out _);
            FlagDatabaseEditing.TryCreate(database, FlagScope.Global, null, "Beta", out FlagDefinition _gBeta, out _);
            FlagDatabaseEditing.TryCreate(database, FlagScope.Local, SceneA, "Beta", out FlagDefinition _aBeta, out _);
            FlagDatabaseEditing.TryCreate(database, FlagScope.Global, null, "Alpha", out FlagDefinition _gAlpha, out _);
            // Scene names are only known after a refresh; give the two scenes distinct names directly.
            _aAlpha.SetSceneName("Level1");
            _aBeta.SetSceneName("Level1");
            _bZeta.SetSceneName("Level2");
            FlagDatabaseEditing.Sort(database);

            CollectionAssert.AreEqual(
                new[] { _gAlpha, _gBeta, _aAlpha, _aBeta, _bZeta },
                database.Flags);
        }

        [Test]
        public void TryGet_AfterEdit_FindsNewEntry()
        {
            Assert.IsFalse(database.TryGet("nope", out _));
            FlagDatabaseEditing.TryCreate(database, FlagScope.Global, null, "A", out FlagDefinition _a, out _);

            Assert.IsTrue(database.TryGet(_a.Guid, out FlagDefinition _found));
            Assert.AreSame(_a, _found);
        }

        [Test]
        public void SetInitialValueAndDescription_UpdateFlag()
        {
            FlagDatabaseEditing.TryCreate(database, FlagScope.Global, null, "A", out FlagDefinition _a, out _);

            FlagDatabaseEditing.SetInitialValue(database, _a.Guid, true);
            FlagDatabaseEditing.SetDescription(database, _a.Guid, "Opens the door");

            Assert.IsTrue(_a.InitialValue);
            Assert.AreEqual("Opens the door", _a.Description);
        }

        [Test]
        public void TryMoveToScene_ChangesSceneAndRejectsClash()
        {
            FlagDatabaseEditing.TryCreate(database, FlagScope.Local, SceneA, "Door01", out FlagDefinition _a, out _);
            FlagDatabaseEditing.TryCreate(database, FlagScope.Local, SceneB, "Door01", out _, out _);
            FlagDatabaseEditing.TryCreate(database, FlagScope.Local, SceneA, "Other", out FlagDefinition _other, out _);
            FlagDatabaseEditing.TryCreate(database, FlagScope.Global, null, "G", out FlagDefinition _global, out _);

            Assert.IsFalse(FlagDatabaseEditing.TryMoveToScene(database, _a.Guid, SceneB, out string _error));
            Assert.IsNotEmpty(_error);
            Assert.IsFalse(FlagDatabaseEditing.TryMoveToScene(database, _global.Guid, SceneB, out _));

            const string SceneC = "cccccccccccccccccccccccccccccccc";
            Assert.IsTrue(FlagDatabaseEditing.TryMoveToScene(database, _other.Guid, SceneC, out _error), _error);
            Assert.AreEqual(SceneC, _other.SceneGuid);
        }

        [Test]
        public void IsNameTaken_HonoursScopeSceneAndException()
        {
            FlagDatabaseEditing.TryCreate(database, FlagScope.Local, SceneA, "Door01", out FlagDefinition _a, out _);

            Assert.IsTrue(FlagDatabaseEditing.IsNameTaken(database, FlagScope.Local, SceneA, "door01", null));
            Assert.IsFalse(FlagDatabaseEditing.IsNameTaken(database, FlagScope.Local, SceneA, "door01", _a.Guid));
            Assert.IsFalse(FlagDatabaseEditing.IsNameTaken(database, FlagScope.Local, SceneB, "door01", null));
            Assert.IsFalse(FlagDatabaseEditing.IsNameTaken(database, FlagScope.Global, null, "door01", null));
        }
    }
}
