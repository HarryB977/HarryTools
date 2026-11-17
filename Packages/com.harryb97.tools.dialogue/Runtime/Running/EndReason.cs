namespace HarryTools.Dialogue {
    /// <summary> Why a <see cref="DialogueRun"/> ended </summary>
    public enum EndReason {
        /// <summary> The dialogue reached an end node or an unconnected output </summary>
        Completed,
        /// <summary> The run was stopped from code, or silenced </summary>
        Stopped,
        /// <summary> The run was skipped to its end </summary>
        Skipped
    }
}
