using UnityEngine;

namespace HarryTools.Audio {
    /// <summary> A sound effect. </summary>
    [CreateAssetMenu(menuName = "HarryTools/Audio/Sound", fileName = "Sound")]
    public sealed class SoundAsset : AudioAsset {
        public SoundAsset() : base(AudioBus.SFX, false, 50) { }
    }
}
