using System.Collections;
using System.Collections.Generic;
using HarryTools.Actions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarryTools.Flags.Tests
{
    public class FlagListenerTests
    {
        // ==================== VARIABLES ====================

        private FlagDatabase database;
        private FlagRef flag;
        private readonly List<GameObject> created = new List<GameObject>();

        // ==================== SETUP ====================

        [SetUp]
        public void SetUp()
        {
            Flags.ClearForTests();
            RecordingAction.Log.Clear();
            RecordingAction.LastSelf = null;
            database = ScriptableObject.CreateInstance<FlagDatabase>();
            FlagDefinition _definition = new FlagDefinition("listener-test-guid", "Door", FlagScope.Global, null, null);
            database.MutableFlags.Add(_definition);
            database.Invalidate();
            Flags.UseDatabaseForTests(database);
            flag = new FlagRef(_definition.Guid, "Global / Door");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject _object in created) if (_object != null) Object.DestroyImmediate(_object);
            created.Clear();
            Flags.ClearForTests();
            Object.DestroyImmediate(database);
            RecordingAction.Log.Clear();
            RecordingAction.LastSelf = null;
        }

        // ==================== HELPERS ====================

        private static ActionList List(string _tag, int _frames = 0, bool _disableSelf = false)
        {
            return new ActionList(new RecordingAction(_tag, _frames, _disableSelf));
        }

        private static ActionList Empty() => new ActionList();

        private FlagListener Make(ActionList _applySet, ActionList _applyCleared, ActionList _changeSet, ActionList _changeCleared)
        {
            GameObject _object = new GameObject("FlagListenerTest");
            created.Add(_object);
            _object.SetActive(false);
            FlagListener _listener = _object.AddComponent<FlagListener>();
            _listener.SetUpForTests(flag, _applySet, _applyCleared, _changeSet, _changeCleared);
            _object.SetActive(true);
            return _listener;
        }

        private FlagListener MakeDefault() => Make(List("applySet"), List("applyCleared"), List("changeSet"), List("changeCleared"));

        // ==================== TESTS ====================

        [Test]
        public void Enable_AppliesCurrentValue_WithApplyList()
        {
            MakeDefault();
            CollectionAssert.AreEqual(new[] { "applyCleared", "applyCleared:done" }, RecordingAction.Log);

            flag.Set();
            RecordingAction.Log.Clear();
            MakeDefault();
            CollectionAssert.AreEqual(new[] { "applySet", "applySet:done" }, RecordingAction.Log);
        }

        [Test]
        public void LiveChange_RunsChangeList()
        {
            MakeDefault();
            RecordingAction.Log.Clear();

            flag.Set();
            CollectionAssert.AreEqual(new[] { "changeSet", "changeSet:done" }, RecordingAction.Log);

            RecordingAction.Log.Clear();
            flag.Clear();
            CollectionAssert.AreEqual(new[] { "changeCleared", "changeCleared:done" }, RecordingAction.Log);
        }

        [Test]
        public void LiveChange_EmptyChangeList_FallsBackToApply()
        {
            Make(List("applySet"), List("applyCleared"), Empty(), List("changeCleared"));
            RecordingAction.Log.Clear();

            flag.Set();
            CollectionAssert.AreEqual(new[] { "applySet", "applySet:done" }, RecordingAction.Log);

            RecordingAction.Log.Clear();
            flag.Clear();
            CollectionAssert.AreEqual(new[] { "changeCleared", "changeCleared:done" }, RecordingAction.Log);
        }

        [Test]
        public void Restore_RunsApplyList_NotChangeList()
        {
            MakeDefault();
            RecordingAction.Log.Clear();

            FlagSnapshot _snapshot = new FlagSnapshot();
            _snapshot.entries.Add(new FlagSnapshotEntry { guid = flag.Guid, type = "bool", value = "true" });
            Flags.Restore(_snapshot);
            CollectionAssert.AreEqual(new[] { "applySet", "applySet:done" }, RecordingAction.Log);

            RecordingAction.Log.Clear();
            Flags.ResetAll();
            CollectionAssert.AreEqual(new[] { "applyCleared", "applyCleared:done" }, RecordingAction.Log);
        }

        [Test]
        public void Listener_RestoreAfterEnable_ReappliesWithApplyList()
        {
            flag.Set();
            MakeDefault();
            CollectionAssert.AreEqual(new[] { "applySet", "applySet:done" }, RecordingAction.Log);

            RecordingAction.Log.Clear();
            Flags.Restore(new FlagSnapshot());
            CollectionAssert.AreEqual(new[] { "applyCleared", "applyCleared:done" }, RecordingAction.Log);

            RecordingAction.Log.Clear();
            Flags.Restore(new FlagSnapshot());
            CollectionAssert.IsEmpty(RecordingAction.Log, "A restore that changes nothing must not rerun anything.");
        }

        [Test]
        public void ChangeWhileDisabled_NoRun_ThenEnableApplies()
        {
            FlagListener _listener = MakeDefault();
            _listener.gameObject.SetActive(false);
            RecordingAction.Log.Clear();

            flag.Set();
            CollectionAssert.IsEmpty(RecordingAction.Log);

            _listener.gameObject.SetActive(true);
            CollectionAssert.AreEqual(new[] { "applySet", "applySet:done" }, RecordingAction.Log);
        }

        [UnityTest]
        public IEnumerator NewChange_CancelsRunningList()
        {
            Make(List("applySet"), List("applyCleared"), List("changeSet", 5), List("changeCleared"));
            RecordingAction.Log.Clear();

            flag.Set();
            yield return null;
            CollectionAssert.AreEqual(new[] { "changeSet" }, RecordingAction.Log);

            flag.Clear();
            CollectionAssert.AreEqual(new[] { "changeSet", "changeCleared", "changeCleared:done" }, RecordingAction.Log);

            for (int _i = 0; _i < 8; _i++) yield return null;
            CollectionAssert.DoesNotContain(RecordingAction.Log, "changeSet:done");
        }

        [UnityTest]
        public IEnumerator Disable_CancelsRunningList()
        {
            FlagListener _listener = Make(List("applySet"), List("applyCleared"), List("changeSet", 5), List("changeCleared"));
            RecordingAction.Log.Clear();

            flag.Set();
            yield return null;
            _listener.gameObject.SetActive(false);

            for (int _i = 0; _i < 8; _i++) yield return null;
            CollectionAssert.AreEqual(new[] { "changeSet" }, RecordingAction.Log);
        }

        [UnityTest]
        public IEnumerator ActionChangesOwnFlag_DisableLeavesNoListRunning()
        {
            // The set list clears the listener's own flag synchronously, starting the cleared list inside the set list's start.
            ActionList _changeSet = new ActionList(new ClearFlagAction(new FlagRef(flag.Guid, flag.Label)), new RecordingAction("changeSet", 5));
            FlagListener _listener = Make(List("applySet"), List("applyCleared"), _changeSet, List("changeCleared", 5));
            RecordingAction.Log.Clear();

            flag.Set();
            yield return null;
            CollectionAssert.Contains(RecordingAction.Log, "changeCleared");
            _listener.gameObject.SetActive(false);

            for (int _i = 0; _i < 8; _i++) yield return null;
            CollectionAssert.DoesNotContain(RecordingAction.Log, "changeCleared:done", "The newer list must be the one disable cancels.");
            CollectionAssert.DoesNotContain(RecordingAction.Log, "changeSet:done");
        }

        [UnityTest]
        public IEnumerator ActionDisablesOwnObject_NoException()
        {
            FlagListener _listener = Make(List("applySet"), List("applyCleared"), List("changeSet", 3, true), List("changeCleared"));
            RecordingAction.Log.Clear();

            flag.Set();
            for (int _i = 0; _i < 6; _i++) yield return null;

            Assert.IsFalse(_listener.gameObject.activeSelf);
            CollectionAssert.AreEqual(new[] { "changeSet" }, RecordingAction.Log);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Context_SelfIsListenerGameObject()
        {
            FlagListener _listener = MakeDefault();
            Assert.AreSame(_listener.gameObject, RecordingAction.LastSelf);
            Assert.AreSame(flag, _listener.Flag);
        }
    }
}
