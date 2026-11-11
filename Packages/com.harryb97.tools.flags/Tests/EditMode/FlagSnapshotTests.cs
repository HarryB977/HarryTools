using System.Collections.Generic;
using System.Text.RegularExpressions;
using HarryTools.Flags.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarryTools.Flags.Tests
{
    public class FlagSnapshotTests
    {
        // ==================== VARIABLES ====================

        private FlagDatabase database;

        // ==================== SETUP ====================

        [SetUp]
        public void SetUp()
        {
            Flags.ClearForTests();
            database = ScriptableObject.CreateInstance<FlagDatabase>();
            Flags.UseDatabaseForTests(database);
        }

        [TearDown]
        public void TearDown()
        {
            Flags.ClearForTests();
            Object.DestroyImmediate(database);
        }

        // ==================== HELPERS ====================

        private FlagRef Make(string _name, bool _initial = false)
        {
            Assert.IsTrue(FlagDatabaseEditing.TryCreate(database, FlagScope.Global, null, _name, out FlagDefinition _created, out string _error), _error);
            if (_initial) FlagDatabaseEditing.SetInitialValue(database, _created.Guid, true);
            return new FlagRef(_created.Guid, "Global / " + _name);
        }

        // ==================== TESTS ====================

        [Test]
        public void Capture_OnlyChangedFlags()
        {
            FlagRef _a = Make("A");
            Make("B");
            Make("C");
            _a.Set();

            FlagSnapshot _snapshot = Flags.Capture();

            Assert.AreEqual(1, _snapshot.entries.Count);
            Assert.AreEqual(_a.Guid, _snapshot.entries[0].guid);
            Assert.AreEqual("bool", _snapshot.entries[0].type);
            Assert.AreEqual("true", _snapshot.entries[0].value);
        }

        [Test]
        public void Restore_ReplacesState_FiresPerChangedFlag_ThenRestored()
        {
            FlagRef _a = Make("A");
            FlagRef _b = Make("B");
            _a.Set();
            FlagSnapshot _snapshot = new FlagSnapshot();
            _snapshot.entries.Add(new FlagSnapshotEntry { guid = _b.Guid, value = "true" });

            List<string> _order = new List<string>();
            List<FlagChange> _changes = new List<FlagChange>();
            Flags.Changed += _change => { _order.Add("changed"); _changes.Add(_change); };
            Flags.Restored += () => _order.Add("restored");

            Flags.Restore(_snapshot);

            Assert.IsFalse(_a.IsSet);
            Assert.IsTrue(_b.IsSet);
            Assert.AreEqual(2, _changes.Count);
            foreach (FlagChange _change in _changes) Assert.AreEqual(FlagChangeReason.Restore, _change.Reason);
            CollectionAssert.AreEqual(new[] { "changed", "changed", "restored" }, _order);
        }

        [Test]
        public void Restore_SameValue_NoEvent()
        {
            FlagRef _a = Make("A");
            FlagRef _b = Make("B");
            _a.Set();
            FlagSnapshot _snapshot = Flags.Capture();
            _b.Set();
            _b.Clear();

            List<FlagChange> _changes = new List<FlagChange>();
            Flags.Changed += _changes.Add;
            Flags.Restore(_snapshot);

            Assert.AreEqual(0, _changes.Count);
            Assert.IsTrue(_a.IsSet);
            Assert.IsFalse(_b.IsSet);
        }

        [Test]
        public void Restore_UnknownGuid_IgnoredWithWarning()
        {
            FlagRef _a = Make("A");
            FlagSnapshot _snapshot = new FlagSnapshot();
            _snapshot.entries.Add(new FlagSnapshotEntry { guid = "ffffffffffffffffffffffffffffffff", value = "true" });
            _snapshot.entries.Add(new FlagSnapshotEntry { guid = _a.Guid, value = "true" });

            LogAssert.Expect(LogType.Warning, new Regex("Flags"));
            Flags.Restore(_snapshot);

            Assert.IsTrue(_a.IsSet);
        }

        [Test]
        public void Restore_UnknownType_IgnoredWithWarning()
        {
            FlagRef _a = Make("A");
            FlagSnapshot _snapshot = new FlagSnapshot();
            _snapshot.entries.Add(new FlagSnapshotEntry { guid = _a.Guid, type = "int", value = "3" });

            LogAssert.Expect(LogType.Warning, new Regex("Flags"));
            Flags.Restore(_snapshot);

            Assert.IsFalse(_a.IsSet);
            Assert.IsFalse(Flags.HasOverride(_a.Guid));
        }

        [Test]
        public void Restore_Null_ResetsToInitial()
        {
            FlagRef _a = Make("A", true);
            FlagRef _b = Make("B");
            _a.Clear();
            _b.Set();

            Flags.Restore(null);

            Assert.IsTrue(_a.IsSet);
            Assert.IsFalse(_b.IsSet);
        }

        [Test]
        public void ResetAll_FiresReset_ThenRestored()
        {
            FlagRef _a = Make("A");
            _a.Set();
            List<string> _order = new List<string>();
            FlagChangeReason _reason = FlagChangeReason.Live;
            Flags.Changed += _change => { _order.Add("changed"); _reason = _change.Reason; };
            Flags.Restored += () => _order.Add("restored");

            Flags.ResetAll();

            Assert.IsFalse(_a.IsSet);
            Assert.AreEqual(FlagChangeReason.Reset, _reason);
            CollectionAssert.AreEqual(new[] { "changed", "restored" }, _order);
            Assert.AreEqual(0, Flags.Capture().entries.Count);
        }

        [Test]
        public void Rename_KeepsGuid_SnapshotStillApplies()
        {
            FlagRef _a = Make("Door01");
            _a.Set();
            FlagSnapshot _snapshot = Flags.Capture();

            Assert.IsTrue(FlagDatabaseEditing.TryRename(database, _a.Guid, "Gate01", out string _error), _error);
            Flags.ResetAll();
            Assert.IsFalse(_a.IsSet);
            Flags.Restore(_snapshot);

            Assert.IsTrue(_a.IsSet);
        }

        [Test]
        public void Snapshot_JsonRoundTrip()
        {
            FlagRef _a = Make("A");
            FlagRef _b = Make("B");
            _a.Set();
            _b.Set();

            string _json = JsonUtility.ToJson(Flags.Capture());
            FlagSnapshot _loaded = JsonUtility.FromJson<FlagSnapshot>(_json);

            Assert.AreEqual(1, _loaded.version);
            Assert.AreEqual(2, _loaded.entries.Count);
            Flags.ResetAll();
            Flags.Restore(_loaded);
            Assert.IsTrue(_a.IsSet);
            Assert.IsTrue(_b.IsSet);
        }
    }
}
