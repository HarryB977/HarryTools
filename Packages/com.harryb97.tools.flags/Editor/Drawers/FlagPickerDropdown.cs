using System;
using System.Collections.Generic;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace HarryTools.Flags.Editor
{
    /// <summary>Dropdown item that carries the flag it stands for. A null flag is the "Create new…" entry.</summary>
    internal sealed class FlagPickerItem : AdvancedDropdownItem
    {
        public FlagDefinition Flag { get; }
        public bool IsCreateNew => Flag == null;

        public FlagPickerItem(string _name, FlagDefinition _flag) : base(_name) { Flag = _flag; }
    }

    /// <summary>Searchable picker with a Global group and a group for the locals of the host scene.</summary>
    internal sealed class FlagPickerDropdown : AdvancedDropdown
    {
        // ==================== VARIABLES ====================

        internal const string GlobalGroup = "Global";
        internal const string CreateNewLabel = "Create new…";

        private readonly FlagDatabase database;
        private readonly string hostSceneGuid;
        private readonly Action<FlagDefinition> onSelected;
        private readonly Action onCreateNew;

        // ==================== CONSTRUCTORS ====================

        /// <param name="_onCreateNew">Called when "Create new…" is chosen. Null hides that entry.</param>
        public FlagPickerDropdown(AdvancedDropdownState _state, FlagDatabase _database, string _hostSceneGuid, Action<FlagDefinition> _onSelected, Action _onCreateNew)
            : base(_state)
        {
            database = _database;
            hostSceneGuid = _hostSceneGuid;
            onSelected = _onSelected;
            onCreateNew = _onCreateNew;
            minimumSize = new Vector2(minimumSize.x, 280f);
        }

        // ==================== ADVANCED DROPDOWN ====================

        protected override AdvancedDropdownItem BuildRoot() => BuildTree(database, hostSceneGuid, onCreateNew != null);

        protected override void ItemSelected(AdvancedDropdownItem _item)
        {
            if (!(_item is FlagPickerItem _picked)) return;
            if (_picked.IsCreateNew) onCreateNew?.Invoke();
            else onSelected?.Invoke(_picked.Flag);
        }

        // ==================== INTERNAL ====================

        /// <summary>Builds the Global group, then the host scene group when there is one, then optionally "Create new…".</summary>
        internal static AdvancedDropdownItem BuildTree(FlagDatabase _database, string _hostSceneGuid, bool _includeCreateNew)
        {
            AdvancedDropdownItem _root = new AdvancedDropdownItem("Flags");
            AdvancedDropdownItem _global = new AdvancedDropdownItem(GlobalGroup);
            AdvancedDropdownItem _local = null;

            var _globals = new List<FlagDefinition>();
            var _locals = new List<FlagDefinition>();
            foreach (FlagDefinition _flag in FlagHostScope.Selectable(_database, _hostSceneGuid))
                (_flag.Scope == FlagScope.Global ? _globals : _locals).Add(_flag);

            _globals.Sort(CompareByName);
            _locals.Sort(CompareByName);

            foreach (FlagDefinition _flag in _globals) _global.AddChild(new FlagPickerItem(_flag.Name, _flag));
            _root.AddChild(_global);

            if (!string.IsNullOrEmpty(_hostSceneGuid))
            {
                _local = new AdvancedDropdownItem(SceneGroupName(_locals));
                foreach (FlagDefinition _flag in _locals) _local.AddChild(new FlagPickerItem(_flag.Name, _flag));
                _root.AddChild(_local);
            }

            if (_includeCreateNew) _root.AddChild(new FlagPickerItem(CreateNewLabel, null));
            return _root;
        }

        // ==================== PRIVATE ====================

        private static int CompareByName(FlagDefinition _a, FlagDefinition _b) => string.Compare(_a.Name, _b.Name, StringComparison.OrdinalIgnoreCase);

        private static string SceneGroupName(List<FlagDefinition> _locals)
        {
            foreach (FlagDefinition _flag in _locals)
                if (!string.IsNullOrEmpty(_flag.SceneName)) return _flag.SceneName;
            return "This scene";
        }
    }
}
