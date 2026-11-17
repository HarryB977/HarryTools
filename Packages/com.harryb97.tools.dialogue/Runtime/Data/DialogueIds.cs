using System;

namespace HarryTools.Dialogue {
    /// <summary> Source of the identifiers stored in dialogue assets </summary>
    public static class DialogueIds {
        /// <summary> A new unique identifier </summary>
        public static string New() {
            return Guid.NewGuid().ToString("N");
        }
    }
}
