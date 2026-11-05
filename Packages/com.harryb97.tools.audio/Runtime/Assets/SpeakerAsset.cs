using System.Collections.Generic;
using HarryTools.SerializeReferences;
using UnityEngine;

namespace HarryTools.Audio {
    /// <summary> A character that owns voice lines. </summary>
    [CreateAssetMenu(menuName = "HarryTools/Audio/Speaker", fileName = "Speaker")]
    public sealed class SpeakerAsset : ScriptableObject {
        // ==================== VARIABLES ===================
        [SerializeField] private string displayName;
        [SerializeField, Tooltip("Effect applied to every line of this speaker.")]
        private AudioEffectPreset defaultEffect;
        [SerializeReference, TypeSelector] private List<IAudioAssetExtension> extensions = new List<IAudioAssetExtension>();

        // ==================== PROPERTIES ===================
        public string DisplayName => displayName;
        public AudioEffectPreset DefaultEffect => defaultEffect;

        // ==================== PUBLIC ===================
        /// <summary> Returns the first extension of type T, or null. </summary>
        public T GetExtension<T>() where T : class, IAudioAssetExtension {
            for (int _i = 0; _i < extensions.Count; _i++) {
                if (extensions[_i] is T _match) return _match;
            }
            return null;
        }

        // ==================== INTERNAL ===================
        internal void SetDisplayName(string _name) => displayName = _name;
        internal void SetDefaultEffect(AudioEffectPreset _effect) => defaultEffect = _effect;
        internal void AddExtension(IAudioAssetExtension _extension) => extensions.Add(_extension);
    }
}
