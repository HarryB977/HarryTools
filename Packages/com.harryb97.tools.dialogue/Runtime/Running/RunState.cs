namespace HarryTools.Dialogue {
    /// <summary> Where a <see cref="DialogueRun"/> is in its life </summary>
    public enum RunState {
        Running,
        WaitingChoice,
        Paused,
        Ended
    }
}
