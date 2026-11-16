namespace HarryTools.TextEffects {
    /// <summary> When an effect runs during the life of a character </summary>
    public enum EffectKind {
        Appear,
        Continuous,
        OneShot,
        Disappear
    }

    /// <summary> Direction of a transition effect. In runs as an appear effect, Out as a disappear effect </summary>
    public enum TransitionDirection {
        In,
        Out
    }
}
