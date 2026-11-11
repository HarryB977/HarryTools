using System.Collections.Generic;
using HarryTools.Flags.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarryTools.Flags.Tests
{
    public class FlagsStateTests
    {
        // ==================== VARIABLES ====================

        private FlagDatabase database;
        private ScriptableObject owner;

        // ==================== SETUP ====================

        [SetUp]
        public void SetUp()
        {
            Flags.ClearForTests();
            database = ScriptableObject.CreateInstance<FlagDatabase>();
            owner = ScriptableObject.CreateInstance<FlagDatabase>();
            Flags.UseDatabaseForTests(database);
        }

        [TearDown]
        public void TearDown()
        {
            Flags.ClearForTests();
            Object.DestroyImmediate(database);
            Object.DestroyImmediate(owner);
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
        public void Unset_ReturnsInitialValue()
        {
            FlagRef _on = Make("On", true);
            FlagRef _off = Make("Off");

            Assert.IsTrue(_on.IsSet);
            Assert.IsFalse(_off.IsSet);
        }

        [Test]
        public void Set_ChangesValue_FiresChangedLive()
        {
            FlagRef _flag = Make("Door");
            List<FlagChange> _received = new List<FlagChange>();
            Flags.Changed += _received.Add;

            _flag.Set(owner);

            Assert.IsTrue(_flag.IsSet);
            Assert.AreEqual(1, _received.Count);
            Assert.AreEqual(_flag.Guid, _received[0].Guid);
            Assert.IsFalse(_received[0].OldValue);
            Assert.IsTrue(_received[0].NewValue);
            Assert.AreEqual(FlagChangeReason.Live, _received[0].Reason);
            Assert.AreSame(owner, _received[0].Source);
            Assert.AreEqual("Door", _received[0].Flag.Name);
        }

        [Test]
        public void Set_SameValue_NoEvent()
        {
            FlagRef _flag = Make("Door", true);
            int _count = 0;
            Flags.Changed += _ => _count++;

            _flag.Set();
            _flag.SetValue(true);

            Assert.AreEqual(0, _count);
            Assert.IsFalse(Flags.HasOverride(_flag.Guid));
        }

        [Test]
        public void Toggle_Flips()
        {
            FlagRef _flag = Make("Door");

            _flag.Toggle();
            Assert.IsTrue(_flag.IsSet);
            _flag.Toggle();
            Assert.IsFalse(_flag.IsSet);
        }

        [Test]
        public void Subscribe_ReceivesOnlyOwnFlag()
        {
            FlagRef _a = Make("A");
            FlagRef _b = Make("B");
            List<FlagChange> _received = new List<FlagChange>();
            _a.Subscribe(_received.Add);

            _b.Set();
            _a.Set();

            Assert.AreEqual(1, _received.Count);
            Assert.AreEqual(_a.Guid, _received[0].Guid);
        }

        [Test]
        public void Unsubscribe_StopsNotifications()
        {
            FlagRef _flag = Make("A");
            int _count = 0;
            System.Action<FlagChange> _callback = _ => _count++;
            _flag.Subscribe(_callback);
            _flag.Unsubscribe(_callback);

            _flag.Set();

            Assert.AreEqual(0, _count);
        }

        [Test]
        public void Unsubscribe_DuringCallback_OthersStillNotified()
        {
            FlagRef _flag = Make("A");
            int _secondCount = 0;
            System.Action<FlagChange> _first = null;
            _first = _ => _flag.Unsubscribe(_first);
            _flag.Subscribe(_first);
            _flag.Subscribe(_ => _secondCount++);

            _flag.Set();

            Assert.AreEqual(1, _secondCount);
        }

        [Test]
        public void EmptyRef_IsSetFalse_WriteNoopWarnsOnce()
        {
            FlagRef _empty = new FlagRef();

            Assert.IsFalse(_empty.IsSet);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Flags"));
            _empty.Set();
            _empty.Set();

            Assert.IsFalse(_empty.IsSet);
        }

        [Test]
        public void UnknownGuid_IsSetFalse_WarnsOnce()
        {
            FlagRef _gone = new FlagRef("00000000000000000000000000000000", "Global / Gone");

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Flags"));
            Assert.IsFalse(_gone.IsSet);
            Assert.IsFalse(_gone.IsSet);
            _gone.Set();

            Assert.IsFalse(_gone.IsSet);
        }

        [Test]
        public void Read_EventCarriesReader()
        {
            FlagRef _flag = Make("A", true);
            List<FlagRead> _reads = new List<FlagRead>();
            Flags.Read += _reads.Add;

            Assert.IsTrue(_flag.Read(owner));

            Assert.AreEqual(1, _reads.Count);
            Assert.AreEqual(_flag.Guid, _reads[0].Guid);
            Assert.IsTrue(_reads[0].Value);
            Assert.AreSame(owner, _reads[0].Reader);
        }
    }
}
