using HarryTools.Dialogue.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HarryTools.Dialogue.Tests {
    public class DialoguePlayModeResetTests {
        // ==================== SETUP ====================
        #region Setup
        private DialogueSettings settings;
        private DialogueAsset asset;

        [SetUp]
        public void SetUp() {
            Dialogue.ClearForTests();
            DialogueMemory.ClearForTests();
            settings = ScriptableObject.CreateInstance<DialogueSettings>();
            DialogueSettings.UseForTests(settings);
            asset = ScriptableObject.CreateInstance<DialogueAsset>();
            asset.Start.Next.Target = asset.AddNode<LineNode>().Id;
        }

        [TearDown]
        public void TearDown() {
            Dialogue.ClearForTests();
            DialogueSettings.ClearForTests();
            DialogueMemory.ClearForTests();
            Object.DestroyImmediate(asset);
            Object.DestroyImmediate(settings);
        }
        #endregion

        // ==================== TESTS ====================
        #region Tests
        [Test]
        public void EnteredEditMode_DropsRuns_MainCanStartAgain() {
            Dialogue.ManualTick = true;
            DialogueRun _main = Dialogue.Start(asset);
            Dialogue.Start(asset, new DialogueStartOptions { IsMain = false });
            int _ended = 0;
            Dialogue.Ended += (_run, _reason) => _ended++;
            Assert.AreSame(_main, Dialogue.Main);

            DialoguePlayModeReset.OnPlayModeStateChanged(PlayModeStateChange.EnteredEditMode);

            Assert.IsNull(Dialogue.Main);
            Assert.AreEqual(0, Dialogue.Active.Count);
            Dialogue.ManualTick = true;
            DialogueRun _next = Dialogue.Start(asset);
            Assert.IsNotNull(_next, "the main slot is free");
            Assert.AreSame(_next, Dialogue.Main);
            _next.Stop();
            Assert.AreEqual(0, _ended, "no handler is kept");
        }
        #endregion
    }
}
