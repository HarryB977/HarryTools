namespace HarryTools.Audio {
    /// <summary> Logical output channel. Each bus maps to a mixer group. </summary>
    public enum AudioBus { Master, Music, SFX, Ambience, Voice, UI }

    /// <summary> How a sound picks its next clip. </summary>
    public enum ClipSelection { Random, RandomNoRepeat, Shuffle, Sequence }

    /// <summary> What happens when a sound reaches its maximum number of simultaneous instances. </summary>
    public enum InstanceLimitPolicy { StealOldest, StealQuietest, Ignore }
}
