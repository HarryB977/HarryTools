using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace HarryTools.Dialogue.Tests {
    public class AmbientPolicyTests {
        // ==================== SETUP ====================
        #region Setup
        private readonly List<DialogueAsset> assets = new List<DialogueAsset>();
        private readonly Dictionary<DialogueRun, EndReason> ended = new Dictionary<DialogueRun, EndReason>();
        private DialogueSettings settings;
        private FakeLineChannel channel;

        [SetUp]
        public void SetUp() {
            RecordingAction.Log.Clear();
            DialogueMemory.ClearForTests();
            settings = ScriptableObject.CreateInstance<DialogueSettings>();
            DialogueSettings.UseForTests(settings);
            Dialogue.ManualTick = true;
            channel = new FakeLineChannel();
            Dialogue.ChannelResolver = _ => channel;
            ended.Clear();
            Dialogue.Ended += OnEnded;
        }

        [TearDown]
        public void TearDown() {
            Dialogue.Ended -= OnEnded;
            Dialogue.StopAll();
            Dialogue.ClearForTests();
            DialogueSettings.ClearForTests();
            DialogueMemory.ClearForTests();
            for (int _i = 0; _i < assets.Count; _i++) Object.DestroyImmediate(assets[_i]);
            assets.Clear();
            Object.DestroyImmediate(settings);
        }

        private void OnEnded(DialogueRun _run, EndReason _reason) => ended[_run] = _reason;
        #endregion

        // ==================== HELPERS ====================
        #region Helpers
        /// <summary> Start → one line </summary>
        private DialogueAsset SingleLine(string _label) {
            DialogueAsset _asset = ScriptableObject.CreateInstance<DialogueAsset>();
            assets.Add(_asset);
            LineNode _line = _asset.AddNode<LineNode>();
            _line.WriterNote = _label;
            _asset.Start.Next.Target = _line.Id;
            return _asset;
        }

        /// <summary> An ambient run whose asset overrides the ambient policy when one is given </summary>
        private DialogueRun StartAmbient(string _label, AmbientPolicy? _assetPolicy = null) {
            DialogueAsset _asset = SingleLine(_label);
            if (_assetPolicy.HasValue) {
                _asset.OverrideAmbientPolicy = true;
                _asset.AmbientPolicy = _assetPolicy.Value;
            }
            return Dialogue.Start(_asset, new DialogueStartOptions { IsMain = false });
        }

        private FakeLineHandle HandleOf(DialogueRun _run) {
            for (int _i = channel.Handles.Count - 1; _i >= 0; _i--) {
                if (channel.Handles[_i].Presentation.Run == _run) return channel.Handles[_i];
            }
            return null;
        }

        private static void Tick(float _dt = 0f) => Dialogue.TickAll(_dt, _dt);
        #endregion

        // ==================== TESTS ====================
        #region Tests
        [Test]
        public void Ambient_Continue_Pause_Silence() {
            DialogueRun _continue = StartAmbient("continue", AmbientPolicy.Continue);
            DialogueRun _pause = StartAmbient("pause", AmbientPolicy.Pause);
            DialogueRun _silence = StartAmbient("silence", AmbientPolicy.Silence);
            Tick();

            DialogueRun _main = Dialogue.Start(SingleLine("main"));
            Assert.IsNotNull(_main);

            Assert.AreEqual(RunState.Running, _continue.State);
            Assert.IsFalse(HandleOf(_continue).Paused);
            Assert.AreEqual(RunState.Paused, _pause.State);
            Assert.IsTrue(HandleOf(_pause).Paused);
            Assert.AreEqual(RunState.Ended, _silence.State);
            Assert.AreEqual(EndReason.Stopped, ended[_silence]);
            Assert.IsTrue(HandleOf(_silence).Released);
            CollectionAssert.DoesNotContain(Dialogue.Active, _silence);

            // A paused ambient run does not move while the main one plays
            HandleOf(_pause).Advance();
            HandleOf(_continue).Advance();
            Tick();
            Assert.AreEqual(RunState.Paused, _pause.State);
            Assert.AreEqual(RunState.Ended, _continue.State);
            Assert.AreEqual(EndReason.Completed, ended[_continue]);
        }

        [Test]
        public void Ambient_PauseResumesWhenMainEnds() {
            DialogueRun _pause = StartAmbient("pause", AmbientPolicy.Pause);
            DialogueRun _pausedByHand = StartAmbient("byHand", AmbientPolicy.Pause);
            Tick();
            _pausedByHand.Pause();

            DialogueRun _main = Dialogue.Start(SingleLine("main"));
            Assert.AreEqual(RunState.Paused, _pause.State);

            _main.Stop();
            Assert.AreEqual(RunState.Running, _pause.State);
            Assert.IsFalse(HandleOf(_pause).Paused);
            // Paused before the main dialogue started: stays paused
            Assert.AreEqual(RunState.Paused, _pausedByHand.State);

            // Again, with a main dialogue that completes
            DialogueRun _second = Dialogue.Start(SingleLine("second"));
            Assert.AreEqual(RunState.Paused, _pause.State);
            Tick();
            HandleOf(_second).Advance();
            Tick();
            Assert.AreEqual(EndReason.Completed, ended[_second]);
            Assert.AreEqual(RunState.Running, _pause.State);

            HandleOf(_pause).Advance();
            Tick();
            Assert.AreEqual(EndReason.Completed, ended[_pause]);
        }

        [Test]
        public void ResumeAll_DuringMain_KeepsPolicyPausedAmbients() {
            DialogueRun _ambient = StartAmbient("ambient", AmbientPolicy.Pause);
            DialogueRun _pausedByHand = StartAmbient("byHand", AmbientPolicy.Pause);
            Tick();
            _pausedByHand.Pause();

            DialogueRun _main = Dialogue.Start(SingleLine("main"));
            Tick();
            Assert.AreEqual(RunState.Paused, _ambient.State);

            Dialogue.PauseAll();
            Assert.AreEqual(RunState.Paused, _main.State);
            Dialogue.ResumeAll();
            Assert.AreEqual(RunState.Running, _main.State);
            Assert.AreEqual(RunState.Paused, _ambient.State);
            Assert.IsTrue(HandleOf(_ambient).Paused);
            // Its pause by hand is lifted, the main run's policy is not
            Assert.AreEqual(RunState.Paused, _pausedByHand.State);

            // Resume on the run itself does not lift the policy either
            _ambient.Resume();
            HandleOf(_ambient).Advance();
            Tick();
            Assert.AreEqual(RunState.Paused, _ambient.State);
            Assert.IsTrue(HandleOf(_ambient).Paused);

            // Paused by hand during the main run: the end of the main run lifts only the policy
            _ambient.Pause();
            _main.Stop();
            Assert.AreEqual(RunState.Paused, _ambient.State);
            Assert.AreEqual(RunState.Running, _pausedByHand.State);
            Assert.IsFalse(HandleOf(_pausedByHand).Paused);

            _ambient.Resume();
            Assert.AreEqual(RunState.Running, _ambient.State);
            Assert.IsFalse(HandleOf(_ambient).Paused);
            Tick();
            Assert.AreEqual(EndReason.Completed, ended[_ambient]);
        }

        [Test]
        public void AmbientPolicy_Precedence_Options_Asset_Settings() {
            settings.DefaultAmbientPolicy = AmbientPolicy.Silence;
            DialogueRun _fromAsset = StartAmbient("asset", AmbientPolicy.Pause);
            DialogueRun _fromSettings = StartAmbient("settings");
            // Without the override flag the asset's policy is not used
            DialogueAsset _notOverriddenAsset = SingleLine("notOverridden");
            _notOverriddenAsset.AmbientPolicy = AmbientPolicy.Pause;
            DialogueRun _notOverridden = Dialogue.Start(_notOverriddenAsset, new DialogueStartOptions { IsMain = false });
            Tick();

            // No option on the main call: asset override, then settings
            DialogueRun _main = Dialogue.Start(SingleLine("main"));
            Assert.AreEqual(RunState.Paused, _fromAsset.State);
            Assert.AreEqual(RunState.Ended, _fromSettings.State);
            Assert.AreEqual(EndReason.Stopped, ended[_fromSettings]);
            Assert.AreEqual(RunState.Ended, _notOverridden.State);
            _main.Stop();
            Assert.AreEqual(RunState.Running, _fromAsset.State);

            // The main call's option wins over the asset override and the settings
            DialogueRun _silenceAsset = StartAmbient("silenceAsset", AmbientPolicy.Silence);
            DialogueRun _defaults = StartAmbient("defaults");
            Tick();
            _main = Dialogue.Start(SingleLine("continueMain"), new DialogueStartOptions { AmbientPolicy = AmbientPolicy.Continue });
            Assert.AreEqual(RunState.Running, _fromAsset.State);
            Assert.AreEqual(RunState.Running, _silenceAsset.State);
            Assert.AreEqual(RunState.Running, _defaults.State);
            _main.Stop();

            Dialogue.Start(SingleLine("pauseMain"), new DialogueStartOptions { AmbientPolicy = AmbientPolicy.Pause });
            Assert.AreEqual(RunState.Paused, _fromAsset.State);
            Assert.AreEqual(RunState.Paused, _silenceAsset.State);
            Assert.AreEqual(RunState.Paused, _defaults.State);
        }
        #endregion
    }
}
