using Cysharp.Threading.Tasks;
using UnityEngine;

namespace HarryTools.Audio {
    /// <summary> Stores bus volumes as JSON in PlayerPrefs. </summary>
    public sealed class PlayerPrefsAudioStore : IAudioSettingsStore {
        // ==================== VARIABLES ===================
        private readonly string key;

        // ==================== CONSTRUCTOR ===================
        public PlayerPrefsAudioStore(string _key) => key = _key;

        // ==================== PUBLIC ===================
        public UniTask<AudioVolumeData?> Load() {
            if (!PlayerPrefs.HasKey(key)) return UniTask.FromResult<AudioVolumeData?>(null);
            string _json = PlayerPrefs.GetString(key);
            if (string.IsNullOrEmpty(_json)) return UniTask.FromResult<AudioVolumeData?>(null);
            return UniTask.FromResult<AudioVolumeData?>(JsonUtility.FromJson<AudioVolumeData>(_json));
        }

        public UniTask Save(AudioVolumeData _data) {
            PlayerPrefs.SetString(key, JsonUtility.ToJson(_data));
            PlayerPrefs.Save();
            return UniTask.CompletedTask;
        }
    }
}
