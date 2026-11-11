using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using HarryTools.Actions;
using HarryTools.Conditions;
using HarryTools.Flags.Editor;
using NUnit.Framework;
using UnityEngine;

namespace HarryTools.Flags.Tests
{
    public class FlagLogicTests
    {
        // ==================== VARIABLES ====================

        private FlagDatabase database;
        private GameObject self;
        private LogicContext context;

        // ==================== SETUP ====================

        [SetUp]
        public void SetUp()
        {
            Flags.ClearForTests();
            database = ScriptableObject.CreateInstance<FlagDatabase>();
            Flags.UseDatabaseForTests(database);
            self = new GameObject("FlagLogicSelf");
            context = new LogicContext(self);
        }

        [TearDown]
        public void TearDown()
        {
            Flags.ClearForTests();
            Object.DestroyImmediate(database);
            Object.DestroyImmediate(self);
        }

        // ==================== HELPERS ====================

        private FlagRef Make(string _name)
        {
            Assert.IsTrue(FlagDatabaseEditing.TryCreate(database, FlagScope.Global, null, _name, out FlagDefinition _created, out string _error), _error);
            return new FlagRef(_created.Guid, "Global / " + _name);
        }

        private void Run(IAction _action) => _action.Run(context, CancellationToken.None);

        // ==================== TESTS ====================

        [Test]
        public void FlagIsSet_True_WhenSet()
        {
            FlagRef _flag = Make("Door");
            FlagIsSet _condition = new FlagIsSet(_flag);

            Assert.IsFalse(_condition.Check(context));
            _flag.Set();
            Assert.IsTrue(_condition.Check(context));
        }

        [Test]
        public void FlagIsSet_Invert_ReturnsFalse_WhenSet()
        {
            FlagRef _flag = Make("Door");
            FlagIsSet _condition = new FlagIsSet(_flag);
            typeof(Condition).GetField("invert", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_condition, true);
            _flag.Set();

            Assert.IsFalse(_condition.Check(context));
        }

        [Test]
        public void SetFlag_SetsAndSourceIsSelf()
        {
            FlagRef _flag = Make("Door");
            List<FlagChange> _changes = new List<FlagChange>();
            Flags.Changed += _changes.Add;

            Run(new SetFlagAction(_flag));

            Assert.IsTrue(_flag.IsSet);
            Assert.AreEqual(1, _changes.Count);
            Assert.AreEqual(self, _changes[0].Source);
        }

        [Test]
        public void ClearFlag_Clears()
        {
            FlagRef _flag = Make("Door");
            _flag.Set();

            Run(new ClearFlagAction(_flag));

            Assert.IsFalse(_flag.IsSet);
        }

        [Test]
        public void ToggleFlag_Flips()
        {
            FlagRef _flag = Make("Door");
            ToggleFlagAction _action = new ToggleFlagAction(_flag);

            Run(_action);
            Assert.IsTrue(_flag.IsSet);
            Run(_action);
            Assert.IsFalse(_flag.IsSet);
        }

        [Test]
        public void FlagIsSet_ReportsReaderSelf()
        {
            FlagRef _flag = Make("Door");
            List<FlagRead> _reads = new List<FlagRead>();
            Flags.Read += _reads.Add;

            new FlagIsSet(_flag).Check(context);

            Assert.AreEqual(1, _reads.Count);
            Assert.AreEqual(self, _reads[0].Reader);
        }
    }
}
