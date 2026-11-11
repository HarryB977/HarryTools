using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarryTools.Flags.Tests
{
    public class FlagsLocalSaveTests
    {
        // ==================== VARIABLES ====================

        private const string Directory = "__FlagsTests__";
        private const string File = "flags-save";

        private FlagDatabase database;
        private FlagRef door;
        private FlagRef lamp;

        // ==================== SETUP ====================

        [SetUp]
        public void SetUp()
        {
            Flags.ClearForTests();
            database = ScriptableObject.CreateInstance<FlagDatabase>();
            FlagDefinition _door = new FlagDefinition("save-test-door", "Door", FlagScope.Global, null, null);
            FlagDefinition _lamp = new FlagDefinition("save-test-lamp", "Lamp", FlagScope.Global, null, null);
            database.MutableFlags.Add(_door);
            database.MutableFlags.Add(_lamp);
            database.Invalidate();
            Flags.UseDatabaseForTests(database);
            door = new FlagRef(_door.Guid, "Global / Door");
            lamp = new FlagRef(_lamp.Guid, "Global / Lamp");
            DeleteDirectory();
        }

        [TearDown]
        public void TearDown()
        {
            Flags.ClearForTests();
            Object.DestroyImmediate(database);
            DeleteDirectory();
        }

        private static void DeleteDirectory()
        {
            string _path = $"{Application.persistentDataPath}/{Directory}";
            if (System.IO.Directory.Exists(_path)) System.IO.Directory.Delete(_path, true);
        }

        // ==================== TESTS ====================

        [UnityTest]
        public IEnumerator SaveThenLoad_RestoresValues() => UniTask.ToCoroutine(async () =>
        {
            door.Set();
            Assert.IsTrue(await FlagsLocalSave.SaveAsync(Directory, File));

            door.Clear();
            lamp.Set();
            Assert.IsTrue(await FlagsLocalSave.LoadAsync(Directory, File));

            Assert.IsTrue(door.IsSet);
            Assert.IsFalse(lamp.IsSet);
        });

        [UnityTest]
        public IEnumerator Load_NoFile_ReturnsFalse_StateUnchanged() => UniTask.ToCoroutine(async () =>
        {
            door.Set();

            Assert.IsFalse(await FlagsLocalSave.LoadAsync(Directory, File));

            Assert.IsTrue(door.IsSet);
            Assert.IsFalse(lamp.IsSet);
        });
    }
}
