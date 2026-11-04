using System;
using System.Collections.Generic;
using HarryTools.DomainReload;
using UnityEngine;

namespace HarryTools.Audio {
    /// <summary> Auto-maintained list of every audio asset in the project, looked up by stable id. </summary>
    [CreateAssetMenu(menuName = "HarryTools/Audio/Catalog", fileName = "AudioCatalog")]
    public sealed class AudioCatalog : ScriptableObject {
        // ==================== VARIABLES ===================
        [SerializeField] private List<AudioCatalogItem> items = new List<AudioCatalogItem>();
        [NonSerialized] private Dictionary<string, AudioCatalogItem> lookup;

        // ==================== PROPERTIES ===================
        /// <summary> Catalog used to resolve asset references at runtime. </summary>
        [field: ClearOnReload] internal static AudioCatalog Active { get; set; }

        public IReadOnlyList<AudioCatalogItem> Items => items;

        // ==================== UNITY ===================
        private void OnEnable() => lookup = null;

        // ==================== PUBLIC ===================
        public bool TryGet(string _id, out AudioCatalogItem _item) {
            _item = null;
            if (string.IsNullOrEmpty(_id)) return false;
            if (lookup == null) BuildLookup();
            return lookup.TryGetValue(_id, out _item);
        }

        public bool TryGet<T>(string _id, out T _item) where T : AudioCatalogItem {
            if (TryGet(_id, out AudioCatalogItem _found) && _found is T _typed) {
                _item = _typed;
                return true;
            }
            _item = null;
            return false;
        }

        // ==================== INTERNAL ===================
        internal void SetItems(List<AudioCatalogItem> _items) {
            items = _items;
            lookup = null;
        }

        // ==================== PRIVATE ===================
        private void BuildLookup() {
            lookup = new Dictionary<string, AudioCatalogItem>(items.Count);
            for (int _i = 0; _i < items.Count; _i++) {
                AudioCatalogItem _item = items[_i];
                if (_item == null || string.IsNullOrEmpty(_item.Id)) continue;
                lookup[_item.Id] = _item;
            }
        }
    }
}
