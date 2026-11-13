using System.Collections.Generic;
using HarryTools.Flags.Editor;
using NUnit.Framework;
using UnityEngine;

namespace HarryTools.Flags.Tests
{
    public class FlagWarningsTests
    {
        // ==================== VARIABLES ====================

        private const string MissingSceneGuid = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        private FlagDatabase database;
        private FlagUsageIndex index;
        private string existingSceneGuid;
        private int useCounter;

        // ==================== SETUP ====================

        [SetUp]
        public void SetUp()
        {
            database = ScriptableObject.CreateInstance<FlagDatabase>();
            index = ScriptableObject.CreateInstance<FlagUsageIndex>();
            existingSceneGuid = UnityEditor.AssetDatabase.AssetPathToGUID("Packages/com.harryb97.tools.flags/package.json");
            useCounter = 0;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(database);
            Object.DestroyImmediate(index);
        }

        // ==================== TESTS ====================

        [Test]
        public void Warnings_ReadNeverSet()
        {
            FlagDefinition _flag = Add("g1", "Door", FlagScope.Global, string.Empty);
            Use("g1", FlagUsageKind.Read);

            Assert.That(FlagWarnings.For(_flag, database, index), Is.EqualTo(new[] { "Read but never set" }));
        }

        [Test]
        public void Warnings_SetNeverRead()
        {
            FlagDefinition _flag = Add("g1", "Door", FlagScope.Global, string.Empty);
            Use("g1", FlagUsageKind.Write);

            Assert.That(FlagWarnings.For(_flag, database, index), Is.EqualTo(new[] { "Set but never read" }));
        }

        [Test]
        public void Warnings_Unused()
        {
            FlagDefinition _flag = Add("g1", "Door", FlagScope.Global, string.Empty);

            Assert.That(FlagWarnings.For(_flag, database, index), Is.EqualTo(new[] { "Not used anywhere" }));
        }

        [Test]
        public void Warnings_ReadAndWritten_None()
        {
            FlagDefinition _flag = Add("g1", "Door", FlagScope.Global, string.Empty);
            Use("g1", FlagUsageKind.Read);
            Use("g1", FlagUsageKind.Write);

            Assert.That(FlagWarnings.For(_flag, database, index), Is.Empty);
        }

        [Test]
        public void Warnings_OtherUsageOnly_NoReadWriteWarning()
        {
            FlagDefinition _flag = Add("g1", "Door", FlagScope.Global, string.Empty);
            Use("g1", FlagUsageKind.Other);

            Assert.That(FlagWarnings.For(_flag, database, index), Is.Empty);
        }

        [Test]
        public void Warnings_SameNameOtherScope()
        {
            FlagDefinition _global = Add("g1", "Door", FlagScope.Global, string.Empty);
            FlagDefinition _local = Add("l1", "door", FlagScope.Local, existingSceneGuid);
            Use("g1", FlagUsageKind.Read);
            Use("g1", FlagUsageKind.Write);
            Use("l1", FlagUsageKind.Read);
            Use("l1", FlagUsageKind.Write);

            Assert.That(FlagWarnings.For(_global, database, index), Is.EqualTo(new[] { "A local flag with the same name exists in Scene" }));
            Assert.That(FlagWarnings.For(_local, database, index), Is.EqualTo(new[] { "A global flag with the same name exists" }));
        }

        [Test]
        public void Warnings_MissingScene()
        {
            FlagDefinition _flag = Add("l1", "Door", FlagScope.Local, MissingSceneGuid);
            Use("l1", FlagUsageKind.Read);
            Use("l1", FlagUsageKind.Write);

            Assert.That(FlagWarnings.For(_flag, database, index), Is.EqualTo(new[] { "Scene is missing" }));
        }

        [Test]
        public void Warnings_NullIndex_SkipsUsageWarnings()
        {
            FlagDefinition _flag = Add("g1", "Door", FlagScope.Global, string.Empty);

            Assert.That(FlagWarnings.For(_flag, database, null), Is.Empty);
        }

        // ==================== HELPERS ====================

        private FlagDefinition Add(string _guid, string _name, FlagScope _scope, string _sceneGuid)
        {
            FlagDefinition _flag = new FlagDefinition(_guid, _name, _scope, _sceneGuid, "Scene");
            database.MutableFlags.Add(_flag);
            database.Invalidate();
            return _flag;
        }

        private void Use(string _flagGuid, FlagUsageKind _kind)
        {
            useCounter++;
            index.ReplaceScene("scene" + useCounter, new List<FlagUsageEntry>
            {
                new FlagUsageEntry { flagGuid = _flagGuid, kind = _kind, hierarchyPath = "Host", componentType = "Thing" }
            });
        }
    }
}
