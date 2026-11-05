using Cysharp.Threading.Tasks;

namespace HarryTools.Audio {
    /// <summary> Where bus volumes are saved between sessions. Implement it to plug in another save system. </summary>
    public interface IAudioSettingsStore {
        /// <summary> Saved data, or null when nothing was saved yet. </summary>
        UniTask<AudioVolumeData?> Load();
        UniTask Save(AudioVolumeData _data);
    }
}
