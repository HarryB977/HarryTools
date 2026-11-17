using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using HarryTools.Conditions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarryTools.Dialogue.Tests {
    public class LauncherAndLogicTests {
        // ==================== SETUP ====================
        #region Setup
        private readonly List<DialogueAsset> assets = new List<DialogueAsset>();
        private readonly List<GameObject> objects = new List<GameObject>();
        private DialogueSettings settings;
        private FakeLineChannel channel;

        [SetUp]
        public void SetUp() {
            DialogueMemory.ClearForTests();
            settings = ScriptableObject.CreateInstance<DialogueSettings>();
            DialogueSettings.UseForTests(settings);
            Dialogue.ManualTick = true;
            channel = new FakeLineChannel();
            Dialogue.ChannelResolver = _ => channel;
        }

        [TearDown]
        public void TearDown() {
            Dialogue.StopAll();
            Dialogue.ClearForTests();
            DialogueSettings.ClearForTests();
            DialogueMemory.ClearForTests();
            for (int _i = 0; _i < objects.Count; _i++) Object.DestroyImmediate(objects[_i]);
            objects.Clear();
            for (int _i = 0; _i < assets.Count; _i++) Object.DestroyImmediate(assets[_i]);
            assets.Clear();
            Object.DestroyImmediate(settings);
        }
        #endregion

        // ==================== HELPERS ====================
        #region Helpers
        private static void Tick() => Dialogue.TickAll(0f, 0f);

        /// <summary> Start then a line that waits for input, then the end </summary>
        private DialogueAsset NewLineAsset() {
            DialogueAsset _asset = ScriptableObject.CreateInstance<DialogueAsset>();
            assets.Add(_asset);
            LineNode _line = _asset.AddNode<LineNode>();
            _asset.EnsureIds();
            _asset.Start.Next.Target = _line.Id;
            return _asset;
        }

        private DialogueAsset NewChoiceAsset(out ChoiceOption _option) {
            DialogueAsset _asset = ScriptableObject.CreateInstance<DialogueAsset>();
            assets.Add(_asset);
            _option = new ChoiceOption();
            ChoiceNode _choice = _asset.AddNode<ChoiceNode>();
            _choice.Options.Add(_option);
            _asset.EnsureIds();
            _asset.Start.Next.Target = _choice.Id;
            return _asset;
        }

        private DialogueLauncher NewLauncher(DialogueAsset _asset, LaunchOn _launchOn, bool _active = true) {
            GameObject _object = new GameObject("Launcher");
            objects.Add(_object);
            _object.SetActive(false);
            DialogueLauncher _launcher = _object.AddComponent<DialogueLauncher>();
            _launcher.Dialogue = _asset;
            _launcher.LaunchOn = _launchOn;
            if (_active) _object.SetActive(true);
            return _launcher;
        }
        #endregion

        // ==================== LAUNCHER ====================
        #region Launcher
        [Test]
        public void Launcher_OnStart_Launches_Manual_DoesNot() {
            DialogueLauncher _onStart = NewLauncher(NewLineAsset(), LaunchOn.Manual, false);
            _onStart.LaunchOn = LaunchOn.Start;
            DialogueLauncher _manual = NewLauncher(NewLineAsset(), LaunchOn.Manual);
            _manual.IsMain = false;

            // Start runs on the first frame an active object exists
            _onStart.gameObject.SetActive(true);
            _onStart.SendMessage("Start");

            Assert.IsNotNull(_onStart.Run);
            Assert.AreSame(_onStart.gameObject, _onStart.Run.Options.Origin);
            Assert.IsNull(_manual.Run);

            DialogueRun _first = _manual.Launch();
            Assert.IsNotNull(_first);
            Assert.AreSame(_first, _manual.Launch());
            Assert.AreEqual(2, Dialogue.Active.Count);
        }

        [Test]
        public void Launcher_OnEnable_LaunchesEveryEnable() {
            DialogueLauncher _launcher = NewLauncher(NewLineAsset(), LaunchOn.Enable);
            DialogueRun _first = _launcher.Run;
            Assert.IsNotNull(_first);

            _launcher.gameObject.SetActive(false);
            _first.Stop();
            _launcher.gameObject.SetActive(true);

            Assert.IsNotNull(_launcher.Run);
            Assert.AreNotSame(_first, _launcher.Run);
        }

        [Test]
        public void Launcher_Stop_StopsItsRun() {
            DialogueLauncher _launcher = NewLauncher(NewLineAsset(), LaunchOn.Manual);
            DialogueLauncher _other = NewLauncher(NewLineAsset(), LaunchOn.Manual);
            _other.IsMain = false;
            DialogueRun _mine = _launcher.Launch();
            DialogueRun _theirs = _other.Launch();
            EndReason? _reason = null;
            _mine.Ended += (_, _r) => _reason = _r;

            _launcher.Stop();

            Assert.AreEqual(RunState.Ended, _mine.State);
            Assert.AreEqual(EndReason.Stopped, _reason);
            Assert.AreNotEqual(RunState.Ended, _theirs.State);
            Assert.IsNotNull(_launcher.Launch());
        }
        #endregion

        // ==================== CONDITIONS ====================
        #region Conditions
        [Test]
        public void ChoiceWasChosen_True_After_Choose() {
            DialogueAsset _asset = NewChoiceAsset(out ChoiceOption _option);
            ChoiceWasChosen _condition = new ChoiceWasChosen(_asset, _option.Id);
            Assert.IsFalse(_condition.Check(LogicContext.Empty));

            DialogueRun _run = Dialogue.Start(_asset);
            Tick();
            _run.CurrentChoice.Choose(0);

            Assert.IsTrue(_condition.Check(LogicContext.Empty));
            Assert.IsFalse(new ChoiceWasChosen(_asset, "missing").Check(LogicContext.Empty));
        }

        [Test]
        public void DialoguePlayCount_Comparison() {
            DialogueAsset _asset = NewLineAsset();
            Assert.IsTrue(new DialoguePlayCount(_asset, Comparison.Equal, 0).Check(LogicContext.Empty));

            Dialogue.Start(_asset, new DialogueStartOptions { IsMain = false });
            Dialogue.Start(_asset, new DialogueStartOptions { IsMain = false });

            Assert.IsTrue(new DialoguePlayCount(_asset, Comparison.Equal, 2).Check(LogicContext.Empty));
            Assert.IsTrue(new DialoguePlayCount(_asset, Comparison.GreaterOrEqual, 1).Check(LogicContext.Empty));
            Assert.IsFalse(new DialoguePlayCount(_asset, Comparison.Greater, 2).Check(LogicContext.Empty));
            Assert.IsTrue(new DialoguePlayCount(_asset, Comparison.Less, 3).Check(LogicContext.Empty));
        }

        [Test]
        public void IsMainDialogueRunning_FollowsMain() {
            IsMainDialogueRunning _condition = new IsMainDialogueRunning();
            Assert.IsFalse(_condition.Check(LogicContext.Empty));

            DialogueRun _run = Dialogue.Start(NewLineAsset());
            Assert.IsTrue(_condition.Check(LogicContext.Empty));

            _run.Stop();
            Assert.IsFalse(_condition.Check(LogicContext.Empty));
        }
        #endregion

        // ==================== ACTIONS ====================
        #region Actions
        [Test]
        public void StartDialogueAction_WaitUntilEnd_Waits() {
            DialogueAsset _asset = NewLineAsset();
            GameObject _self = new GameObject("Self");
            GameObject _target = new GameObject("Target");
            objects.Add(_self);
            objects.Add(_target);

            StartDialogueAction _action = new StartDialogueAction(_asset, true, true);
            UniTask _task = _action.Run(new LogicContext(_self, _target), CancellationToken.None);

            Assert.IsNotNull(Dialogue.Main);
            Assert.AreSame(_target, Dialogue.Main.Options.Origin);
            Assert.AreEqual(UniTaskStatus.Pending, _task.Status);

            Dialogue.Main.Stop();
            Assert.AreEqual(UniTaskStatus.Succeeded, _task.Status);
        }

        [Test]
        public void StartDialogueAction_OriginFallsBackToSelf_AndBusyMainFinishes() {
            DialogueAsset _asset = NewLineAsset();
            GameObject _self = new GameObject("Self");
            objects.Add(_self);

            UniTask _first = new StartDialogueAction(_asset, true, false).Run(new LogicContext(_self), CancellationToken.None);
            Assert.AreSame(_self, Dialogue.Main.Options.Origin);
            Assert.AreEqual(UniTaskStatus.Succeeded, _first.Status);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("not started"));
            UniTask _busy = new StartDialogueAction(NewLineAsset(), true, true).Run(new LogicContext(_self), CancellationToken.None);
            Assert.AreEqual(UniTaskStatus.Succeeded, _busy.Status);
        }

        [Test]
        public void StartDialogueAction_Cancel_StopsWaitingNotTheRun() {
            DialogueAsset _asset = NewLineAsset();
            CancellationTokenSource _source = new CancellationTokenSource();

            UniTask _task = new StartDialogueAction(_asset, true, true).Run(LogicContext.Empty, _source.Token);
            DialogueRun _run = Dialogue.Main;
            _source.Cancel();

            Assert.AreEqual(UniTaskStatus.Canceled, _task.Status);
            Assert.AreNotEqual(RunState.Ended, _run.State);
            _source.Dispose();
        }

        [Test]
        public void StopSkipAdvanceActions_ActOnMain() {
            DialogueAsset _asset = NewLineAsset();
            DialogueAsset _ambientAsset = NewLineAsset();
            DialogueRun _ambient = Dialogue.Start(_ambientAsset, new DialogueStartOptions { IsMain = false });
            DialogueRun _main = Dialogue.Start(_asset);
            Tick();

            new AdvanceDialogueAction().Run(LogicContext.Empty, CancellationToken.None);
            new SkipDialogueAction().Run(LogicContext.Empty, CancellationToken.None);
            Tick();
            Tick();
            Assert.AreNotEqual(RunState.Ended, _ambient.State);

            new StopDialogueAction(StopTarget.Main).Run(LogicContext.Empty, CancellationToken.None);
            Assert.AreEqual(RunState.Ended, _main.State);
            Assert.AreNotEqual(RunState.Ended, _ambient.State);

            new StopDialogueAction(StopTarget.Dialogue, _ambientAsset).Run(LogicContext.Empty, CancellationToken.None);
            Assert.AreEqual(RunState.Ended, _ambient.State);
        }

        [Test]
        public void StopAllAction_StopsEveryRun() {
            DialogueRun _a = Dialogue.Start(NewLineAsset());
            DialogueRun _b = Dialogue.Start(NewLineAsset(), new DialogueStartOptions { IsMain = false });

            new StopDialogueAction(StopTarget.All).Run(LogicContext.Empty, CancellationToken.None);

            Assert.AreEqual(RunState.Ended, _a.State);
            Assert.AreEqual(RunState.Ended, _b.State);
        }

        [Test]
        public void ForgetDialogueAction_ClearsMemory() {
            DialogueAsset _asset = NewChoiceAsset(out ChoiceOption _option);
            DialogueRun _run = Dialogue.Start(_asset);
            Tick();
            _run.CurrentChoice.Choose(0);
            Assert.IsTrue(DialogueMemory.WasChosen(_asset, _option.Id));
            Assert.AreEqual(1, DialogueMemory.PlayCount(_asset));

            new ForgetDialogueAction(_asset).Run(LogicContext.Empty, CancellationToken.None);

            Assert.IsFalse(DialogueMemory.WasChosen(_asset, _option.Id));
            Assert.AreEqual(0, DialogueMemory.PlayCount(_asset));
        }
        #endregion
    }
}
