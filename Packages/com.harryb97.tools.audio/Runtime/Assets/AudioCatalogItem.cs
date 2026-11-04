using System.Collections.Generic;
using HarryTools.SerializeReferences;
using UnityEngine;

namespace HarryTools.Audio {
    /// <summary> Base type of everything that appears in the audio catalog. </summary>
    public abstract class AudioCatalogItem : ScriptableObject {
        // ==================== VARIABLES ===================
        [SerializeField, HideInInspector] private string id;
        [SerializeField, Tooltip("Group path used by the catalog and generated code, e.g. Footsteps/Wood. Empty uses the asset folder.")]
        private string groupOverride;
        [SerializeReference, TypeSelector] private List<IAudioAssetExtension> extensions = new List<IAudioAssetExtension>();

        // ==================== PROPERTIES ===================
        /// <summary> Stable 32 hex character identifier. </summary>
        public string Id => id;
        /// <summary> Group path such as "Footsteps/Wood". Empty means the asset folder is used. </summary>
        public string GroupOverride => groupOverride;

        // ==================== PUBLIC ===================
        /// <summary> Returns the first extension of type T, or null. </summary>
        public T GetExtension<T>() where T : class, IAudioAssetExtension {
            for (int _i = 0; _i < extensions.Count; _i++) {
                if (extensions[_i] is T _match) return _match;
            }
            return null;
        }

        // ==================== INTERNAL ===================
        internal void SetId(string _id) => id = _id;
        internal void SetGroupOverride(string _group) => groupOverride = _group;
        internal void AddExtension(IAudioAssetExtension _extension) => extensions.Add(_extension);
    }
}
