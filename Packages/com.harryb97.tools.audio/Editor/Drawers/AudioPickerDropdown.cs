using System;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace HarryTools.Audio.Editor {
    /// <summary> Dropdown item that carries the asset it stands for. A null asset is the None option. </summary>
    internal sealed class AudioPickerItem : AdvancedDropdownItem {
        public AudioCatalogItem Asset { get; }
        public AudioPickerItem(string _name, AudioCatalogItem _asset) : base(_name) { Asset = _asset; }
    }

    /// <summary> Searchable picker grouped by category. The search matches asset names only; group folders are browsed in the tree. </summary>
    internal sealed class AudioPickerDropdown : AdvancedDropdown {
        // ==================== VARIABLES ===================
        private const string NoneLabel = "None";
        private readonly Type itemType;
        private readonly IReadOnlyList<AudioCatalogItem> items;
        private readonly Action<AudioCatalogItem> onSelected;

        // ==================== CONSTRUCTORS ===================
        public AudioPickerDropdown(AdvancedDropdownState _state, Type _itemType, IReadOnlyList<AudioCatalogItem> _items, Action<AudioCatalogItem> _onSelected) : base(_state) {
            itemType = _itemType;
            items = _items;
            onSelected = _onSelected;
            minimumSize = new Vector2(minimumSize.x, 320f);
        }

        // ==================== ADVANCED DROPDOWN ===================
        protected override AdvancedDropdownItem BuildRoot() => BuildTree(itemType, items, AudioCodeGenerator.GetOptions().Root);

        protected override void ItemSelected(AdvancedDropdownItem _item) {
            if (_item is AudioPickerItem _picked) onSelected?.Invoke(_picked.Asset);
        }

        // ==================== INTERNAL ===================
        /// <summary> Builds None plus the assets assignable to the type, nested by group path. </summary>
        internal static AdvancedDropdownItem BuildTree(Type _itemType, IReadOnlyList<AudioCatalogItem> _items, string _root) {
            var _tree = new AdvancedDropdownItem(_itemType != null ? _itemType.Name : "Audio");
            _tree.AddChild(new AudioPickerItem(NoneLabel, null));

            var _sorted = new List<AudioCatalogItem>(_items.Count);
            for (int _i = 0; _i < _items.Count; _i++) {
                AudioCatalogItem _item = _items[_i];
                if (_item == null) continue;
                if (_itemType != null && !_itemType.IsInstanceOfType(_item)) continue;
                _sorted.Add(_item);
            }
            _sorted.Sort((_a, _b) => string.Compare(_a.name, _b.name, StringComparison.OrdinalIgnoreCase));

            var _groups = new Dictionary<string, AdvancedDropdownItem>(StringComparer.Ordinal);
            for (int _i = 0; _i < _sorted.Count; _i++) {
                AudioCatalogItem _item = _sorted[_i];
                AdvancedDropdownItem _parent = GetGroup(_tree, _groups, AudioCodeGenerator.GetGroupPath(_item, _root));
                _parent.AddChild(new AudioPickerItem(_item.name, _item));
            }
            return _tree;
        }

        // ==================== PRIVATE ===================
        private static AdvancedDropdownItem GetGroup(AdvancedDropdownItem _tree, Dictionary<string, AdvancedDropdownItem> _groups, string _path) {
            if (string.IsNullOrWhiteSpace(_path)) return _tree;
            if (_groups.TryGetValue(_path, out AdvancedDropdownItem _existing)) return _existing;
            int _slash = _path.LastIndexOf('/');
            AdvancedDropdownItem _parent = _slash >= 0 ? GetGroup(_tree, _groups, _path.Substring(0, _slash)) : _tree;
            var _group = new AdvancedDropdownItem(_slash >= 0 ? _path.Substring(_slash + 1) : _path);
            _parent.AddChild(_group);
            _groups.Add(_path, _group);
            return _group;
        }
    }
}
