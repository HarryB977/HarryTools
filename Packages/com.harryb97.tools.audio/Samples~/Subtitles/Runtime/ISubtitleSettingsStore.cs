namespace HarryTools.Audio.Subtitles {
    /// <summary> Which kinds of subtitle the player wants to see. </summary>
    public struct SubtitlePreferences {
        // ==================== VARIABLES ===================
        public bool ShowDialogue;
        public bool ShowCaptions;
    }

    /// <summary> Where the player's subtitle options are kept between sessions. Synchronous: it is read once and written on change. </summary>
    public interface ISubtitleSettingsStore {
        /// <summary> The saved options, or null when nothing was saved yet. </summary>
        SubtitlePreferences? Load();
        void Save(SubtitlePreferences _preferences);
    }
}
