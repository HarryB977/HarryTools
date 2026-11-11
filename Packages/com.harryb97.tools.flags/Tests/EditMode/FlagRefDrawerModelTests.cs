using System.Linq;
using HarryTools.Flags.Editor;
using NUnit.Framework;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace HarryTools.Flags.Tests
{
    public class FlagRefDrawerModelTests
    {
        // ==================== VARIABLES ====================

        private const string SceneA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string SceneB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private const string MissingGuid = "ffffffffffffffffffffffffffffffff";

        private FlagDatabase database;
        private FlagDefinition global;
        private FlagDefinition localA;
        private FlagDefinition localB;

        // ==================== SETUP ====================

        [SetUp]
        public void SetUp()
        {
            database = ScriptableObject.CreateInstance<FlagDatabase>();
            FlagDatabaseEditing.TryCreate(database, FlagScope.Global, null, "Gift", out global, out _);
            FlagDatabaseEditing.TryCreate(database, FlagScope.Local, SceneA, "Door01", out localA, out _);
            FlagDatabaseEditing.TryCreate(database, FlagScope.Local, SceneB, "Lights", out localB, out _);
            localA.SetSceneName("Hospital_1");
            localB.SetSceneName("Hospital_2");
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(database);

        // ==================== DISPLAY TEXT ====================

        [Test]
        public void DisplayText_Valid_UsesCurrentPath()
        {
            string _text = FlagRefDrawer.DisplayText(database, localA.Guid, "old label", SceneA, out FlagRefState _state);

            Assert.AreEqual("Hospital_1 / Door01", _text);
            Assert.AreEqual(FlagRefState.Valid, _state);
        }

        [Test]
        public void DisplayText_Empty_IsNone()
        {
            Assert.AreEqual("None", FlagRefDrawer.DisplayText(database, "", "", SceneA, out FlagRefState _state));
            Assert.AreEqual(FlagRefState.Empty, _state);
        }

        [Test]
        public void DisplayText_Deleted_UsesLabelAndMarker()
        {
            string _text = FlagRefDrawer.DisplayText(database, MissingGuid, "Global / Gone", SceneA, out FlagRefState _state);

            Assert.AreEqual("Global / Gone (deleted)", _text);
            Assert.AreEqual(FlagRefState.Deleted, _state);
            Assert.AreEqual("(deleted)", FlagRefDrawer.DisplayText(database, MissingGuid, "", SceneA, out _));
        }

        [Test]
        public void DisplayText_ForeignLocal_ShowsPathAndWarning()
        {
            string _text = FlagRefDrawer.DisplayText(database, localB.Guid, "", SceneA, out FlagRefState _state);

            Assert.AreEqual(FlagRefState.ForeignLocal, _state);
            StringAssert.StartsWith("Hospital_2 / Lights", _text);
            StringAssert.Contains("other scene", _text);
        }

        // ==================== PICKER TREE ====================

        [Test]
        public void BuildTree_InScene_HasGlobalAndOwnSceneGroupsOnly()
        {
            AdvancedDropdownItem _root = FlagPickerDropdown.BuildTree(database, SceneA, false);

            CollectionAssert.AreEqual(new[] { "Global", "Hospital_1" }, _root.children.Select(_c => _c.name).ToArray());
            CollectionAssert.AreEqual(new[] { "Gift" }, _root.children.First().children.Select(_c => _c.name).ToArray());
            CollectionAssert.AreEqual(new[] { "Door01" }, _root.children.Last().children.Select(_c => _c.name).ToArray());
        }

        [Test]
        public void BuildTree_OutsideScene_HasOnlyGlobal()
        {
            AdvancedDropdownItem _root = FlagPickerDropdown.BuildTree(database, null, false);

            CollectionAssert.AreEqual(new[] { "Global" }, _root.children.Select(_c => _c.name).ToArray());
        }

        [Test]
        public void BuildTree_CreateNew_IsLastAndOnlyWhenEnabled()
        {
            Assert.IsFalse(FlagPickerDropdown.BuildTree(database, SceneA, false).children.Any(_c => _c is FlagPickerItem _i && _i.IsCreateNew));

            AdvancedDropdownItem _root = FlagPickerDropdown.BuildTree(database, SceneA, true);
            FlagPickerItem _last = _root.children.Last() as FlagPickerItem;
            Assert.IsNotNull(_last);
            Assert.IsTrue(_last.IsCreateNew);
        }
    }
}
