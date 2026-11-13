namespace HarryTools.Flags.Editor
{
    /// <summary>One change or read in the play-mode flag history. Consecutive identical reads share one entry.</summary>
    internal sealed class FlagHistoryEntry
    {
        // ==================== VARIABLES ====================

        /// <summary>Seconds since startup (<c>Time.realtimeSinceStartupAsDouble</c>) of the latest occurrence.</summary>
        public double Time;

        public string FlagGuid;

        public bool IsRead;

        /// <summary>Value before the change. Equals <see cref="NewValue"/> for reads.</summary>
        public bool OldValue;

        /// <summary>Value after the change, or the value that was read.</summary>
        public bool NewValue;

        public FlagChangeReason Reason;

        public string SourceName;

        public int SourceInstanceId;

        /// <summary>How many identical reads this entry stands for.</summary>
        public int Count = 1;
    }
}
