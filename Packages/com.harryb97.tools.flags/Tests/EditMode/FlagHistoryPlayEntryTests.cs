using System.Collections;
using HarryTools.Flags.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarryTools.Flags.Tests
{
    public class FlagHistoryPlayEntryTests
    {
        // ==================== VARIABLES ====================

        private const string FlagName = "EntryFlag";

        // ==================== TESTS ====================

        [UnityTest]
        public IEnumerator FirstFrameWrite_DuringPlayEntry_IsRecorded()
        {
            // The hook's armed state and database live in statics that only survive entering Play without a domain reload.
            bool _noDomainReload = EditorSettings.enterPlayModeOptionsEnabled
                && (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) != 0;
            Assume.That(_noDomainReload, "Needs Enter Play Mode Options with domain reload disabled.");

            FlagDatabase _database = ScriptableObject.CreateInstance<FlagDatabase>();
            _database.hideFlags = HideFlags.HideAndDontSave;
            Assert.IsTrue(FlagDatabaseEditing.TryCreate(_database, FlagScope.Global, null, FlagName, out FlagDefinition _created, out string _error), _error);

            bool _recorded = false;
            bool _ran = false;
            PlayEntryHook.Database = _database;
            PlayEntryHook.FlagGuid = _created.Guid;
            PlayEntryHook.Armed = true;
            try
            {
                FlagHistory.Clear();

                yield return new EnterPlayMode();
                PlayEntryHook.Armed = false;
                foreach (FlagHistoryEntry _entry in FlagHistory.For(_created.Guid)) _recorded |= !_entry.IsRead && _entry.NewValue;
                _ran = PlayEntryHook.Ran;
                yield return new ExitPlayMode();
            }
            finally
            {
                PlayEntryHook.Armed = false;
                Flags.ClearForTests();
                FlagHistory.Clear();
                Object.DestroyImmediate(_database);
                PlayEntryHook.Database = null;
                PlayEntryHook.Ran = false;
            }

            Assert.That(_ran, Is.True, "The play entry hook did not run.");
            Assert.That(_recorded, Is.True, "A write made before EnteredPlayMode was not recorded.");
        }
    }

    /// <summary>Writes a flag at play start, after the runtime reset and before any Awake, only while armed by a test.</summary>
    internal static class PlayEntryHook
    {
        public static bool Armed;
        public static bool Ran;
        public static FlagDatabase Database;
        public static string FlagGuid;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Run()
        {
            if (!Armed || Database == null) return;
            Ran = true;
            Flags.UseDatabaseForTests(Database);
            Flags.Set(new FlagRef(FlagGuid, "Global / EntryFlag"));
        }
    }
}
