using UnityEngine;

namespace HarryTools.Audio {
    /// <summary> Shortcuts to play an asset directly: <c>footstep.Play(transform)</c>. </summary>
    public static class AudioAssetExtensions {
        public static AudioHandle Play(this SoundAsset _sound) => Audio.Play(_sound);
        public static AudioHandle Play(this SoundAsset _sound, Vector3 _position) => Audio.Play(_sound, _position);
        public static AudioHandle Play(this SoundAsset _sound, Transform _follow) => Audio.Play(_sound, _follow);
    }
}
