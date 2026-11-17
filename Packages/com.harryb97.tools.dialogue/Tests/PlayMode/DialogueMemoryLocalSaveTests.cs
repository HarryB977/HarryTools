using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarryTools.Dialogue.Tests {
    public class DialogueMemoryLocalSaveTests {
        // ==================== SETUP ====================
        #region Setup
        private const string Directory = "__DialogueMemoryTests__";
        private const string File = "dialogue-memory";

        private DialogueAsset dialogue;

        [SetUp]
        public void SetUp() {
            DialogueMemory.ClearForTests();
            dialogue = ScriptableObject.CreateInstance<DialogueAsset>();
            dialogue.SetAssetIdForTests("memory-save-test");
            DeleteDirectory();
        }

        [TearDown]
        public void TearDown() {
            DialogueMemory.ClearForTests();
            Object.DestroyImmediate(dialogue);
            DeleteDirectory();
        }

        private static void DeleteDirectory() {
            string _path = $"{Application.persistentDataPath}/{Directory}";
            if (System.IO.Directory.Exists(_path)) System.IO.Directory.Delete(_path, true);
        }
        #endregion

        // ==================== TESTS ====================
        #region Tests
        [UnityTest]
        public IEnumerator LocalSave_RoundTrip() => UniTask.ToCoroutine(async () => {
            DialogueMemory.MarkPlayed(dialogue);
            DialogueMemory.MarkChosen(dialogue, "a");
            Assert.IsTrue(await DialogueMemoryLocalSave.SaveAsync(Directory, File));

            DialogueMemory.ResetAll();
            Assert.AreEqual(0, DialogueMemory.PlayCount(dialogue));
            Assert.IsTrue(await DialogueMemoryLocalSave.LoadAsync(Directory, File));

            Assert.AreEqual(1, DialogueMemory.PlayCount(dialogue));
            Assert.IsTrue(DialogueMemory.WasChosen(dialogue, "a"));
        });

        [UnityTest]
        public IEnumerator Load_NoFile_ReturnsFalse_MemoryUnchanged() => UniTask.ToCoroutine(async () => {
            DialogueMemory.MarkPlayed(dialogue);

            Assert.IsFalse(await DialogueMemoryLocalSave.LoadAsync(Directory, File));

            Assert.AreEqual(1, DialogueMemory.PlayCount(dialogue));
        });
        #endregion
    }
}
