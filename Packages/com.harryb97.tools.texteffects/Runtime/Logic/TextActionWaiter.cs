namespace HarryTools.TextEffects {
    /// <summary> Flag raised by an EffectText event, used by the actions that wait on a text </summary>
    internal sealed class TextActionWaiter {
        public bool Done { get; private set; }

        public void Finish() => Done = true;
    }
}
