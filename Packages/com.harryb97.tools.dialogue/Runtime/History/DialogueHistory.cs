using System;
using System.Collections.Generic;
using HarryTools.DomainReload;

namespace HarryTools.Dialogue {
    /// <summary> Optional record of the lines shown and options chosen in main dialogues. Holds data only </summary>
    public static class DialogueHistory {
        // ==================== VARIABLES ===================
        #region Private Variables
        [ClearOnReload(AssignNewInstance = true)] private static List<HistoryEntry> entries = new List<HistoryEntry>();
        [ClearOnReload] private static Action<HistoryEntry> addedHandlers;
        #endregion

        #region Public Variables
        public static IReadOnlyList<HistoryEntry> Entries => entries;
        #endregion

        // ==================== EVENTS ====================
        #region Events
        public static event Action<HistoryEntry> Added {
            add => addedHandlers += value;
            remove => addedHandlers -= value;
        }
        #endregion

        // ==================== METHODS ====================
        #region Public Methods
        public static void Clear() => entries.Clear();
        #endregion

        // ==================== INTERNAL ====================
        #region Internal
        internal static void RecordLine(DialogueRun _run, LineNode _line) {
            if (!ShouldRecord(_run)) return;
            Add(new HistoryEntry(_run.CurrentAsset, _line));
        }

        internal static void RecordChoice(DialogueRun _run, ChoiceOption _option) {
            if (!ShouldRecord(_run)) return;
            Add(new HistoryEntry(_run.CurrentAsset, _option));
        }

        internal static void OnRunEnded(DialogueRun _run) {
            if (!_run.IsMain) return;
            if (DialogueSettings.Current.HistoryClear == HistoryClear.OnConversationEnd) entries.Clear();
        }

        internal static void ClearForTests() {
            entries.Clear();
            addedHandlers = null;
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        private static bool ShouldRecord(DialogueRun _run) {
            return _run.IsMain && DialogueSettings.Current.HistoryEnabled;
        }

        private static void Add(HistoryEntry _entry) {
            entries.Add(_entry);
            int _max = DialogueSettings.Current.HistoryMax;
            if (_max > 0 && entries.Count > _max) entries.RemoveRange(0, entries.Count - _max);
            addedHandlers?.Invoke(_entry);
        }
        #endregion
    }
}
