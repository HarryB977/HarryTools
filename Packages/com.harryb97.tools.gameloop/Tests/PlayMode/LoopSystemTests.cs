using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarryTools.GameLoop.Tests {
    public class LoopSystemTests {
        // ==================== METHODS ====================
        [TearDown]
        public void TearDown() {
            LoopSystem.Reset();
        }

        // =================================================
        [UnityTest]
        public IEnumerator AddLoop_WithoutInitialize_Works() {
            int _calls = 0;
            ILoopable _loop = new Loop("A");
            _loop.AddUpdateListener(() => _calls++);

            LoopSystem.AddLoop(_loop);
            yield return null;

            Assert.Greater(_calls, 0);
        }

        [UnityTest]
        public IEnumerator Initialize_Twice_KeepsLoopsAndSingleManager() {
            LoopSystem.Initialize();
            LoopSystem.AddLoop(new Loop("A"));
            LoopSystem.Initialize();
            yield return null;

            Assert.IsTrue(LoopSystem.HasLoop("A"));
            Assert.AreEqual(1, Object.FindObjectsByType<LoopManager>(FindObjectsSortMode.None).Length);
        }

        [UnityTest]
        public IEnumerator Reset_ClearsLoopsAndManager() {
            LoopSystem.AddLoop(new Loop("A"));
            yield return null;

            LoopSystem.Reset();
            yield return null;

            Assert.AreEqual(0, LoopSystem.Loops.Count);
            Assert.IsFalse(LoopSystem.IsInitialized);
            Assert.AreEqual(0, Object.FindObjectsByType<LoopManager>(FindObjectsSortMode.None).Length);
        }

        [UnityTest]
        public IEnumerator AddLoopFromListener_DoesNotThrow() {
            ILoopable _a = new Loop("A");
            bool _added = false;
            _a.AddUpdateListener(() => {
                if (_added) return;
                _added = true;
                LoopSystem.AddLoop(new Loop("B"));
            });
            LoopSystem.AddLoop(_a);

            yield return null;
            yield return null;

            Assert.IsTrue(LoopSystem.HasLoop("B"));
        }

        [UnityTest]
        public IEnumerator PausedLoop_SkipsListeners() {
            int _calls = 0;
            ILoopable _loop = new Loop("A");
            _loop.AddUpdateListener(() => _calls++);
            _loop.IsPaused = true;
            LoopSystem.AddLoop(_loop);

            yield return null;
            yield return null;

            Assert.AreEqual(0, _calls);
        }
    }
}
