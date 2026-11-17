using System;
using System.Collections.Generic;

namespace HarryTools.Dialogue {
    /// <summary> Saved dialogue memory, compatible with JsonUtility. Holds only the dialogues that have been played </summary>
    [Serializable]
    public sealed class DialogueMemorySnapshot {
        public int version = 1;
        public List<DialogueMemoryEntry> entries = new List<DialogueMemoryEntry>();
    }

    /// <summary> Saved memory of one dialogue </summary>
    [Serializable]
    public sealed class DialogueMemoryEntry {
        public string dialogueId;
        public int playCount;
        public List<string> chosen = new List<string>();
    }
}
