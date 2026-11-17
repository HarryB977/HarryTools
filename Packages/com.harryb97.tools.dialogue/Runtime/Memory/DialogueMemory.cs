using System;
using System.Collections.Generic;
using HarryTools.DomainReload;
using Logger = HarryTools.LoggerTool.Logger;

namespace HarryTools.Dialogue {
    /// <summary> Persistent memory of dialogues: how many times each one was played and which options were chosen </summary>
    public static class DialogueMemory {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const string LogCategory = "Dialogue";
        private const int CurrentVersion = 1;

        private sealed class Record {
            public int playCount;
            public readonly HashSet<string> chosen = new HashSet<string>();
        }

        [ClearOnReload(AssignNewInstance = true)] private static Dictionary<string, Record> records = new Dictionary<string, Record>();
        [ClearOnReload] private static Action restoredHandlers;
        #endregion

        // ==================== EVENTS ====================
        #region Events
        /// <summary> Raised once after Restore or ResetAll finished </summary>
        public static event Action Restored {
            add => restoredHandlers += value;
            remove => restoredHandlers -= value;
        }
        #endregion

        // ==================== QUERIES ====================
        #region Queries
        /// <summary> Times the dialogue was played </summary>
        public static int PlayCount(DialogueAsset _dialogue) {
            if (_dialogue == null) return 0;
            return records.TryGetValue(_dialogue.AssetId, out Record _record) ? _record.playCount : 0;
        }

        /// <summary> Whether the option was ever chosen in the dialogue </summary>
        public static bool WasChosen(DialogueAsset _dialogue, string _optionId) {
            if (_dialogue == null || string.IsNullOrEmpty(_optionId)) return false;
            return records.TryGetValue(_dialogue.AssetId, out Record _record) && _record.chosen.Contains(_optionId);
        }
        #endregion

        // ==================== WRITES ====================
        #region Writes
        /// <summary> Counts one more play of the dialogue </summary>
        public static void MarkPlayed(DialogueAsset _dialogue) {
            if (_dialogue == null) return;
            GetOrCreate(_dialogue.AssetId).playCount++;
        }

        /// <summary> Remembers that the option was chosen in the dialogue </summary>
        public static void MarkChosen(DialogueAsset _dialogue, string _optionId) {
            if (_dialogue == null || string.IsNullOrEmpty(_optionId)) return;
            GetOrCreate(_dialogue.AssetId).chosen.Add(_optionId);
        }

        /// <summary> Erases the memory of one dialogue </summary>
        public static void Forget(DialogueAsset _dialogue) {
            if (_dialogue == null) return;
            records.Remove(_dialogue.AssetId);
        }

        /// <summary> Erases the memory of every dialogue </summary>
        public static void ResetAll() {
            records.Clear();
            restoredHandlers?.Invoke();
        }

        private static Record GetOrCreate(string _id) {
            if (!records.TryGetValue(_id, out Record _record)) {
                _record = new Record();
                records[_id] = _record;
            }
            return _record;
        }
        #endregion

        // ==================== SNAPSHOT ====================
        #region Snapshot
        /// <summary> Saves the memory of every dialogue that has any </summary>
        public static DialogueMemorySnapshot Capture() {
            DialogueMemorySnapshot _snapshot = new DialogueMemorySnapshot { version = CurrentVersion };
            foreach (KeyValuePair<string, Record> _pair in records) {
                DialogueMemoryEntry _entry = new DialogueMemoryEntry { dialogueId = _pair.Key, playCount = _pair.Value.playCount };
                _entry.chosen.AddRange(_pair.Value.chosen);
                _snapshot.entries.Add(_entry);
            }
            return _snapshot;
        }

        /// <summary> Replaces the whole memory with the snapshot. A null snapshot erases everything </summary>
        public static void Restore(DialogueMemorySnapshot _snapshot) {
            Dictionary<string, Record> _next = new Dictionary<string, Record>();
            if (_snapshot != null) {
                if (_snapshot.version > CurrentVersion) {
                    Logger.LogWarning(LogCategory, $"Memory snapshot version {_snapshot.version} is newer than the supported version {CurrentVersion}. Reading it anyway.");
                }
                if (_snapshot.entries != null) {
                    foreach (DialogueMemoryEntry _entry in _snapshot.entries) {
                        if (_entry == null || string.IsNullOrEmpty(_entry.dialogueId)) {
                            Logger.LogWarning(LogCategory, "Memory snapshot entry ignored: it has no dialogue id.");
                            continue;
                        }
                        if (!_next.TryGetValue(_entry.dialogueId, out Record _record)) {
                            _record = new Record();
                            _next[_entry.dialogueId] = _record;
                        }
                        _record.playCount += Math.Max(0, _entry.playCount);
                        if (_entry.chosen == null) continue;
                        foreach (string _optionId in _entry.chosen) {
                            if (!string.IsNullOrEmpty(_optionId)) _record.chosen.Add(_optionId);
                        }
                    }
                }
            }
            records = _next;
            restoredHandlers?.Invoke();
        }
        #endregion

        // ==================== TESTS ====================
        #region Tests
        internal static void ClearForTests() {
            records.Clear();
            restoredHandlers = null;
        }
        #endregion
    }
}
