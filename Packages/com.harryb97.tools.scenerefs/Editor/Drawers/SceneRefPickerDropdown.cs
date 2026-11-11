using System;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace HarryTools.SceneRefs.Editor
{
    /// <summary>Dropdown item that carries the index entry it stands for. The None option has no entry.</summary>
    internal sealed class SceneRefPickerItem : AdvancedDropdownItem
    {
        public bool HasEntry { get; }
        public SceneIdIndexEntry Entry { get; }

        public SceneRefPickerItem(string _name) : base(_name) { }

        public SceneRefPickerItem(string _name, SceneIdIndexEntry _entry) : base(_name)
        {
            HasEntry = true;
            Entry = _entry;
        }
    }

    /// <summary>
    /// Searchable picker over every object of the id index, open and closed scenes alike, grouped by scene.
    /// Lists the entries that have a T, plus those whose component types are unknown.
    /// </summary>
    internal sealed class SceneRefPickerDropdown : AdvancedDropdown
    {
        // ==================== VARIABLES ====================

        private const string NoneLabel = "None";

        private readonly Type targetType;
        private readonly IReadOnlyList<SceneIdIndexEntry> entries;
        private readonly Action<SceneRefPickerItem> onSelected;

        // ==================== CONSTRUCTORS ====================

        public SceneRefPickerDropdown(AdvancedDropdownState _state, Type _targetType, IReadOnlyList<SceneIdIndexEntry> _entries, Action<SceneRefPickerItem> _onSelected) : base(_state)
        {
            targetType = _targetType;
            entries = _entries;
            onSelected = _onSelected;
            minimumSize = new Vector2(minimumSize.x, 320f);
        }

        // ==================== ADVANCED DROPDOWN ====================

        protected override AdvancedDropdownItem BuildRoot() => BuildTree(targetType, entries);

        protected override void ItemSelected(AdvancedDropdownItem _item)
        {
            if (_item is SceneRefPickerItem _picked) onSelected?.Invoke(_picked);
        }

        // ==================== INTERNAL ====================

        /// <summary>None plus one group per scene, each with its matching entries sorted by id.</summary>
        internal static AdvancedDropdownItem BuildTree(Type _targetType, IReadOnlyList<SceneIdIndexEntry> _entries)
        {
            AdvancedDropdownItem _tree = new AdvancedDropdownItem(_targetType != null ? _targetType.Name : "Scene object");
            _tree.AddChild(new SceneRefPickerItem(NoneLabel));
            if (_entries == null) return _tree;

            List<SceneIdIndexEntry> _sorted = new List<SceneIdIndexEntry>(_entries.Count);
            for (int _i = 0; _i < _entries.Count; _i++)
            {
                SceneIdIndexEntry _entry = _entries[_i];
                if (string.IsNullOrEmpty(_entry.Guid)) continue;
                if (_targetType != null && !SceneRefAssign.Matches(_entry, _targetType)) continue;
                _sorted.Add(_entry);
            }
            _sorted.Sort(CompareEntries);

            Dictionary<string, AdvancedDropdownItem> _groups = new Dictionary<string, AdvancedDropdownItem>(StringComparer.Ordinal);
            for (int _i = 0; _i < _sorted.Count; _i++)
            {
                SceneIdIndexEntry _entry = _sorted[_i];
                string _key = _entry.SceneGuid ?? string.Empty;
                if (!_groups.TryGetValue(_key, out AdvancedDropdownItem _group))
                {
                    _group = new AdvancedDropdownItem(string.IsNullOrEmpty(_entry.SceneName) ? "Untitled" : _entry.SceneName);
                    _tree.AddChild(_group);
                    _groups.Add(_key, _group);
                }

                _group.AddChild(new SceneRefPickerItem(_entry.Id, _entry));
            }

            return _tree;
        }

        // ==================== PRIVATE ====================

        private static int CompareEntries(SceneIdIndexEntry _a, SceneIdIndexEntry _b)
        {
            int _scene = string.Compare(_a.SceneName, _b.SceneName, StringComparison.OrdinalIgnoreCase);
            if (_scene != 0) return _scene;
            _scene = string.CompareOrdinal(_a.SceneGuid, _b.SceneGuid);
            return _scene != 0 ? _scene : string.Compare(_a.Id, _b.Id, StringComparison.OrdinalIgnoreCase);
        }
    }
}
