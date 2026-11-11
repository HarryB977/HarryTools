using System;
using System.Collections.Generic;

namespace HarryTools.Flags
{
    /// <summary>
    /// Saved flag state, compatible with JsonUtility. Holds only the flags changed since the state was last reset.
    /// </summary>
    [Serializable]
    public sealed class FlagSnapshot
    {
        public int version = 1;
        public List<FlagSnapshotEntry> entries = new List<FlagSnapshotEntry>();
    }

    /// <summary>One saved flag value. The type field leaves room for non-bool flags later.</summary>
    [Serializable]
    public sealed class FlagSnapshotEntry
    {
        public string guid;
        public string type = "bool";
        public string value;
    }
}
