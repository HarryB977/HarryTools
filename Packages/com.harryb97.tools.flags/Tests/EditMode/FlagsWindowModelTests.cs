using System.Collections.Generic;
using System.Linq;
using HarryTools.Flags.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HarryTools.Flags.Tests
{
    public class FlagsWindowModelTests
    {
        // ==================== VARIABLES ====================

        private const string MissingSceneGuid = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        private FlagDatabase database;
        private FlagUsageIndex index;
        private string sceneGuidA;
        private string sceneGuidB;

        // ==================== SETUP ====================

        [SetUp]
        public void SetUp()
        {
            database = ScriptableObject.CreateInstance<FlagDatabase>();
            index = ScriptableObject.CreateInstance<FlagUsageIndex>();
            // Any existing asset guid stands for a scene that still exists.
            sceneGuidA = AssetDatabase.AssetPathToGUID("Packages/com.harryb97.tools.flags/package.json");
            sceneGuidB = AssetDatabase.AssetPathToGUID("Packages/com.harryb97.tools.flags/README.md");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(database);
            Object.DestroyImmediate(index);
        }

        // ==================== TESTS ====================

        [Test]
        public void Build_GlobalFirst_ThenScenesByName_MissingLast()
        {
            Add("m1", "Lost", FlagScope.Local, MissingSceneGuid, "Gone");
            Add("b1", "Door", FlagScope.Local, sceneGuidB, "Zeta");
            Add("a2", "Window", FlagScope.Local, sceneGuidA, "alpha");
            Add("a1", "Bell", FlagScope.Local, sceneGuidA, "alpha");
            Add("g2", "Gift", FlagScope.Global, string.Empty, string.Empty);
            Add("g1", "Fence", FlagScope.Global, string.Empty, string.Empty);

            List<FlagGroup> _groups = FlagsWindowModel.Build(database, string.Empty, FlagFilter.All);

            Assert.That(_groups.Select(_group => _group.Title), Is.EqualTo(new[] { "Global", "alpha", "Zeta", "Missing scene" }));
            Assert.That(_groups[0].Flags.Select(_flag => _flag.Name), Is.EqualTo(new[] { "Fence", "Gift" }));
            Assert.That(_groups[1].Flags.Select(_flag => _flag.Name), Is.EqualTo(new[] { "Bell", "Window" }));
            Assert.That(_groups[1].SceneGuid, Is.EqualTo(sceneGuidA));
            Assert.That(_groups[1].SceneMissing, Is.False);
            Assert.That(_groups[3].SceneMissing, Is.True);
            Assert.That(_groups[3].Flags.Select(_flag => _flag.Name), Is.EqualTo(new[] { "Lost" }));
        }

        [Test]
        public void Build_SearchIgnoresCase()
        {
            Add("g1", "GiftLeft", FlagScope.Global, string.Empty, string.Empty);
            Add("g2", "Fence", FlagScope.Global, string.Empty, string.Empty);
            Add("a1", "BIGGIFT", FlagScope.Local, sceneGuidA, "Hospital");

            List<FlagGroup> _groups = FlagsWindowModel.Build(database, "gift", FlagFilter.All);

            Assert.That(_groups.SelectMany(_group => _group.Flags).Select(_flag => _flag.Name), Is.EqualTo(new[] { "GiftLeft", "BIGGIFT" }));
        }

        [Test]
        public void Build_FilterLocal_HidesGlobal()
        {
            Add("g1", "Gift", FlagScope.Global, string.Empty, string.Empty);
            Add("a1", "Door", FlagScope.Local, sceneGuidA, "Hospital");
            Add("m1", "Lost", FlagScope.Local, MissingSceneGuid, "Gone");

            List<FlagGroup> _local = FlagsWindowModel.Build(database, string.Empty, FlagFilter.Local);
            List<FlagGroup> _global = FlagsWindowModel.Build(database, string.Empty, FlagFilter.Global);

            Assert.That(_local.Select(_group => _group.Title), Is.EqualTo(new[] { "Hospital", "Missing scene" }));
            Assert.That(_global.Select(_group => _group.Title), Is.EqualTo(new[] { "Global" }));
        }

        [Test]
        public void Build_EmptyGroupsOmitted()
        {
            Add("a1", "Door", FlagScope.Local, sceneGuidA, "Hospital");
            Add("b1", "Lamp", FlagScope.Local, sceneGuidB, "Basement");

            List<FlagGroup> _all = FlagsWindowModel.Build(database, string.Empty, FlagFilter.All);
            List<FlagGroup> _searched = FlagsWindowModel.Build(database, "lamp", FlagFilter.All);

            Assert.That(_all.Select(_group => _group.Title), Is.EqualTo(new[] { "Basement", "Hospital" }));
            Assert.That(_searched.Select(_group => _group.Title), Is.EqualTo(new[] { "Basement" }));
            Assert.That(FlagsWindowModel.Build(null, string.Empty, FlagFilter.All), Is.Empty);
        }

        [Test]
        public void UsageCount_CountsEntries()
        {
            index.ReplaceScene("scene1", new List<FlagUsageEntry>
            {
                new FlagUsageEntry { flagGuid = "g1", kind = FlagUsageKind.Read, hierarchyPath = "A", componentType = "T" },
                new FlagUsageEntry { flagGuid = "g1", kind = FlagUsageKind.Write, hierarchyPath = "B", componentType = "T" },
                new FlagUsageEntry { flagGuid = "g2", kind = FlagUsageKind.Read, hierarchyPath = "C", componentType = "T" }
            });
            index.ReplaceAsset("Assets/Thing.asset", new List<FlagUsageEntry>
            {
                new FlagUsageEntry { flagGuid = "g1", kind = FlagUsageKind.Other, hierarchyPath = "Thing", componentType = "T" }
            });

            Assert.That(FlagsWindowModel.UsageCount(index, "g1"), Is.EqualTo(3));
            Assert.That(FlagsWindowModel.UsageCount(index, "g2"), Is.EqualTo(1));
            Assert.That(FlagsWindowModel.UsageCount(index, "none"), Is.EqualTo(0));
            Assert.That(FlagsWindowModel.UsageCount(null, "g1"), Is.EqualTo(0));
        }

        // ==================== HELPERS ====================

        private void Add(string _guid, string _name, FlagScope _scope, string _sceneGuid, string _sceneName)
        {
            database.MutableFlags.Add(new FlagDefinition(_guid, _name, _scope, _sceneGuid, _sceneName));
            database.Invalidate();
        }
    }
}
