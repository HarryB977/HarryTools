namespace HarryTools.TextEffects {
    /// <summary> Receives what a Typewriter does while it advances </summary>
    public interface ITypewriterListener {
        /// <summary> A character became visible. '_skipped' is true when it was revealed by Skip </summary>
        void OnCharacterShown(int _index, bool _skipped);

        /// <summary> An event command was reached. '_skipped' is true when it was reached by Skip </summary>
        void OnEvent(string _name, int _ordinal, bool _skipped);

        void OnVoiceChanged(string _voiceName);

        void OnWaitingInput();

        void OnCompleted();
    }
}
