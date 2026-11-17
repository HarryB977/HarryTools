using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarryTools.Dialogue.Tests {
    public class DialogueMemoryTests {
        // ==================== SETUP ====================
        #region Setup
        private DialogueAsset first;
        private DialogueAsset second;

        [SetUp]
        public void SetUp() {
            DialogueMemory.ClearForTests();
            first = NewAsset("memory-test-first");
            second = NewAsset("memory-test-second");
        }

        [TearDown]
        public void TearDown() {
            DialogueMemory.ClearForTests();
            Object.DestroyImmediate(first);
            Object.DestroyImmediate(second);
        }

        private static DialogueAsset NewAsset(string _id) {
            DialogueAsset _asset = ScriptableObject.CreateInstance<DialogueAsset>();
            _asset.SetAssetIdForTests(_id);
            return _asset;
        }
        #endregion

        // ==================== TESTS ====================
        #region Tests
        [Test]
        public void MarkPlayed_IncrementsCount() {
            Assert.AreEqual(0, DialogueMemory.PlayCount(first));

            DialogueMemory.MarkPlayed(first);
            DialogueMemory.MarkPlayed(first);

            Assert.AreEqual(2, DialogueMemory.PlayCount(first));
            Assert.AreEqual(0, DialogueMemory.PlayCount(second));
        }

        [Test]
        public void MarkChosen_WasChosen_True_OtherOption_False() {
            DialogueMemory.MarkChosen(first, "a");

            Assert.IsTrue(DialogueMemory.WasChosen(first, "a"));
            Assert.IsFalse(DialogueMemory.WasChosen(first, "b"));
            Assert.IsFalse(DialogueMemory.WasChosen(second, "a"));
        }

        [Test]
        public void Forget_ClearsOnlyThatDialogue() {
            DialogueMemory.MarkPlayed(first);
            DialogueMemory.MarkChosen(first, "a");
            DialogueMemory.MarkPlayed(second);
            DialogueMemory.MarkChosen(second, "b");

            DialogueMemory.Forget(first);

            Assert.AreEqual(0, DialogueMemory.PlayCount(first));
            Assert.IsFalse(DialogueMemory.WasChosen(first, "a"));
            Assert.AreEqual(1, DialogueMemory.PlayCount(second));
            Assert.IsTrue(DialogueMemory.WasChosen(second, "b"));
        }

        [Test]
        public void CaptureRestore_RoundTrip() {
            DialogueMemory.MarkPlayed(first);
            DialogueMemory.MarkPlayed(first);
            DialogueMemory.MarkChosen(first, "a");
            DialogueMemory.MarkChosen(first, "b");
            DialogueMemory.MarkPlayed(second);

            string _json = JsonUtility.ToJson(DialogueMemory.Capture());
            DialogueMemory.ResetAll();
            Assert.AreEqual(0, DialogueMemory.PlayCount(first));

            DialogueMemory.Restore(JsonUtility.FromJson<DialogueMemorySnapshot>(_json));

            Assert.AreEqual(2, DialogueMemory.PlayCount(first));
            Assert.IsTrue(DialogueMemory.WasChosen(first, "a"));
            Assert.IsTrue(DialogueMemory.WasChosen(first, "b"));
            Assert.AreEqual(1, DialogueMemory.PlayCount(second));
            Assert.IsFalse(DialogueMemory.WasChosen(second, "a"));
        }

        [Test]
        public void Restore_UnknownIds_Ignored() {
            DialogueMemory.MarkChosen(first, "a");
            DialogueMemorySnapshot _snapshot = DialogueMemory.Capture();
            _snapshot.entries.Add(new DialogueMemoryEntry { dialogueId = "no-such-dialogue", playCount = 3, chosen = { "x" } });
            _snapshot.entries.Add(null);
            _snapshot.entries.Add(new DialogueMemoryEntry { dialogueId = "" });
            LogAssert.Expect(LogType.Warning, new Regex("no dialogue id"));
            LogAssert.Expect(LogType.Warning, new Regex("no dialogue id"));

            Assert.DoesNotThrow(() => DialogueMemory.Restore(_snapshot));

            Assert.IsTrue(DialogueMemory.WasChosen(first, "a"));
            Assert.IsFalse(DialogueMemory.WasChosen(second, "a"));
        }

        [Test]
        public void Restore_FutureVersion_Warns() {
            DialogueMemorySnapshot _snapshot = new DialogueMemorySnapshot { version = 99 };
            _snapshot.entries.Add(new DialogueMemoryEntry { dialogueId = "memory-test-first", playCount = 4 });
            LogAssert.Expect(LogType.Warning, new Regex("version 99"));

            DialogueMemory.Restore(_snapshot);

            Assert.AreEqual(4, DialogueMemory.PlayCount(first));
        }

        [Test]
        public void Restore_Null_ResetsAll() {
            DialogueMemory.MarkPlayed(first);
            DialogueMemory.MarkChosen(second, "a");

            DialogueMemory.Restore(null);

            Assert.AreEqual(0, DialogueMemory.PlayCount(first));
            Assert.IsFalse(DialogueMemory.WasChosen(second, "a"));
        }

        [Test]
        public void Restored_EventFires() {
            int _count = 0;
            void Handler() => _count++;
            DialogueMemory.Restored += Handler;

            DialogueMemory.Restore(new DialogueMemorySnapshot());
            DialogueMemory.ResetAll();
            DialogueMemory.Restored -= Handler;
            DialogueMemory.ResetAll();

            Assert.AreEqual(2, _count);
        }
        #endregion
    }
}
