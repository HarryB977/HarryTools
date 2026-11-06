using UnityEngine;

namespace HarryTools.Audio.Subtitles {
    /// <summary> Keeps the player's subtitle options in PlayerPrefs. </summary>
    public sealed class PlayerPrefsSubtitleStore : ISubtitleSettingsStore {
        // ==================== VARIABLES ===================
        private readonly string dialogueKey;
        private readonly string captionsKey;

        // ==================== CONSTRUCTOR ===================
        public PlayerPrefsSubtitleStore(string _key = "HarryTools.Audio.Subtitles") {
            dialogueKey = _key + ".Dialogue";
            captionsKey = _key + ".Captions";
        }

        // ==================== PUBLIC ===================
        public SubtitlePreferences? Load() {
            if (!PlayerPrefs.HasKey(dialogueKey) && !PlayerPrefs.HasKey(captionsKey)) return null;
            return new SubtitlePreferences {
                ShowDialogue = PlayerPrefs.GetInt(dialogueKey, 1) != 0,
                ShowCaptions = PlayerPrefs.GetInt(captionsKey, 1) != 0
            };
        }

        public void Save(SubtitlePreferences _preferences) {
            PlayerPrefs.SetInt(dialogueKey, _preferences.ShowDialogue ? 1 : 0);
            PlayerPrefs.SetInt(captionsKey, _preferences.ShowCaptions ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
