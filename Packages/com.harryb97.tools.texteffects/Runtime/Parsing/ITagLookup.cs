namespace HarryTools.TextEffects {
    /// <summary> Tells the parser which tag names belong to registered presets and voices </summary>
    public interface ITagLookup {
        bool IsPreset(string _name);
        bool IsVoice(string _name);
    }
}
