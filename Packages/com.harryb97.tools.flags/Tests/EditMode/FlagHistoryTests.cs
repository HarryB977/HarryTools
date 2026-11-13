using System.Linq;
using HarryTools.Flags.Editor;
using NUnit.Framework;
using UnityEngine;

namespace HarryTools.Flags.Tests
{
    public class FlagHistoryTests
    {
        // ==================== VARIABLES ====================

        private const string Guid = "gggggggggggggggggggggggggggggggg";

        private ScriptableObject source;

        // ==================== SETUP ====================

        [SetUp]
        public void SetUp()
        {
            FlagHistory.Clear();
            source = ScriptableObject.CreateInstance<FlagDatabase>();
            source.name = "Door switch";
        }

        [TearDown]
        public void TearDown()
        {
            FlagHistory.Clear();
            Object.DestroyImmediate(source);
            FlagHistory.Resubscribe();
        }

        // ==================== TESTS ====================

        [Test]
        public void Record_Change_AddsEntry()
        {
            FlagHistory.Record(new FlagChange(Guid, null, false, true, FlagChangeReason.Live, source));

            Assert.That(FlagHistory.All.Count, Is.EqualTo(1));
            FlagHistoryEntry _entry = FlagHistory.All[0];
            Assert.That(_entry.FlagGuid, Is.EqualTo(Guid));
            Assert.That(_entry.IsRead, Is.False);
            Assert.That(_entry.OldValue, Is.False);
            Assert.That(_entry.NewValue, Is.True);
            Assert.That(_entry.SourceName, Is.EqualTo("Door switch"));
            Assert.That(_entry.SourceInstanceId, Is.EqualTo(source.GetInstanceID()));
            Assert.That(_entry.Count, Is.EqualTo(1));
        }

        [Test]
        public void Record_RepeatedRead_Groups()
        {
            for (int _i = 0; _i < 3; _i++) FlagHistory.Record(new FlagRead(Guid, true, source));

            Assert.That(FlagHistory.All.Count, Is.EqualTo(1));
            Assert.That(FlagHistory.All[0].IsRead, Is.True);
            Assert.That(FlagHistory.All[0].Count, Is.EqualTo(3));
        }

        [Test]
        public void Record_RepeatedRead_UpdatesTime()
        {
            FlagHistory.Record(new FlagRead(Guid, true, source));
            double _first = FlagHistory.All[0].Time;
            FlagHistory.All[0].Time = _first - 10;

            FlagHistory.Record(new FlagRead(Guid, true, source));

            Assert.That(FlagHistory.All[0].Time, Is.GreaterThan(_first - 10));
        }

        [Test]
        public void Record_ReadWithOtherValueOrSource_NewEntry()
        {
            FlagHistory.Record(new FlagRead(Guid, true, source));
            FlagHistory.Record(new FlagRead(Guid, false, source));
            FlagHistory.Record(new FlagRead(Guid, false, null));

            Assert.That(FlagHistory.All.Count, Is.EqualTo(3));
        }

        [Test]
        public void Record_ReadAfterChange_NewEntry()
        {
            FlagHistory.Record(new FlagRead(Guid, true, source));
            FlagHistory.Record(new FlagChange(Guid, null, true, false, FlagChangeReason.Live, source));
            FlagHistory.Record(new FlagRead(Guid, false, source));

            Assert.That(FlagHistory.All.Count, Is.EqualTo(3));
            Assert.That(FlagHistory.All[0].IsRead, Is.True);
            Assert.That(FlagHistory.All[1].IsRead, Is.False);
        }

        [Test]
        public void Capacity_DropsOldest()
        {
            for (int _i = 0; _i < FlagHistory.Capacity + 1; _i++)
                FlagHistory.Record(new FlagChange("flag" + _i, null, false, true, FlagChangeReason.Live, source));

            Assert.That(FlagHistory.All.Count, Is.EqualTo(FlagHistory.Capacity));
            Assert.That(FlagHistory.All[0].FlagGuid, Is.EqualTo("flag" + FlagHistory.Capacity));
            Assert.That(FlagHistory.All.Any(_e => _e.FlagGuid == "flag0"), Is.False);
            Assert.That(FlagHistory.All.Any(_e => _e.FlagGuid == "flag1"), Is.True);
        }

        [Test]
        public void Clear_Empties()
        {
            FlagHistory.Record(new FlagChange(Guid, null, false, true, FlagChangeReason.Live, source));

            FlagHistory.Clear();

            Assert.That(FlagHistory.All, Is.Empty);
            Assert.That(FlagHistory.LastChangeTime(Guid), Is.EqualTo(-1));
        }

        [Test]
        public void NullSourceLive_NamedCode()
        {
            FlagHistory.Record(new FlagChange(Guid, null, false, true, FlagChangeReason.Live, null));

            Assert.That(FlagHistory.All[0].SourceName, Is.EqualTo("Code"));
        }

        [Test]
        public void WindowSourceLive_NamedFlagsWindow()
        {
            FlagHistory.Record(new FlagChange(Guid, null, false, true, FlagChangeReason.Live, FlagHistory.WindowSource));

            Assert.That(FlagHistory.All[0].SourceName, Is.EqualTo("Flags window"));
            Assert.That(FlagHistory.WindowSource, Is.SameAs(FlagHistory.WindowSource), "The marker is created once.");
        }

        [Test]
        public void For_FiltersByFlag_AndLastChangeTimeIgnoresReads()
        {
            FlagHistory.Record(new FlagChange(Guid, null, false, true, FlagChangeReason.Live, source));
            FlagHistory.Record(new FlagChange("other", null, false, true, FlagChangeReason.Live, source));
            FlagHistory.Record(new FlagRead(Guid, true, source));

            Assert.That(FlagHistory.For(Guid).Count(), Is.EqualTo(2));
            Assert.That(FlagHistory.LastChangeTime(Guid), Is.EqualTo(FlagHistory.All[2].Time));
            Assert.That(FlagHistory.LastChangeTime("missing"), Is.EqualTo(-1));
        }

        [Test]
        public void Record_RaisesUpdated()
        {
            int _raised = 0;
            void Handler() => _raised++;
            FlagHistory.Updated += Handler;

            FlagHistory.Record(new FlagChange(Guid, null, false, true, FlagChangeReason.Live, source));
            FlagHistory.Clear();

            FlagHistory.Updated -= Handler;
            Assert.That(_raised, Is.EqualTo(2));
        }

        [Test]
        public void Resubscribe_AfterHandlersWiped_RecordsSetOnce()
        {
            FlagDatabase _database = ScriptableObject.CreateInstance<FlagDatabase>();
            try
            {
                Assert.IsTrue(FlagDatabaseEditing.TryCreate(_database, FlagScope.Global, null, "Door", out FlagDefinition _created, out string _error), _error);
                Flags.ClearForTests();
                Flags.UseDatabaseForTests(_database);
                FlagRef _flag = new FlagRef(_created.Guid, "Global / Door");

                FlagHistory.Resubscribe();
                FlagHistory.Resubscribe();
                Flags.Set(_flag, source);

                Assert.That(FlagHistory.All.Count, Is.EqualTo(1));
                Assert.That(FlagHistory.All[0].NewValue, Is.True);

                Flags.IsSet(_flag, source);
                Assert.That(FlagHistory.All.Count, Is.EqualTo(2));
            }
            finally
            {
                Flags.ClearForTests();
                Object.DestroyImmediate(_database);
            }
        }
    }
}
