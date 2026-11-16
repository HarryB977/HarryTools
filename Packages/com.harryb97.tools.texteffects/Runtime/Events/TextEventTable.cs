using System;
using System.Collections.Generic;
using HarryTools.Actions;
using UnityEngine;

namespace HarryTools.TextEffects {
    /// <summary> Serializable list of text events, looked up by name ignoring case </summary>
    [Serializable]
    public sealed class TextEventTable : ITextEventProvider {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private List<TextEventEntry> entries = new List<TextEventEntry>();
        #endregion

        #region Public Variables
        public IReadOnlyList<TextEventEntry> Entries => entries;
        #endregion

        // ==================== CONSTRUCTORS ===================
        public TextEventTable() { }

        public TextEventTable(List<TextEventEntry> _entries) {
            entries = _entries ?? new List<TextEventEntry>();
        }

        // ==================== METHODS ===================
        public bool Contains(string _name) => Find(_name) != null;

        public bool TryGetEvent(string _name, out ActionList _actions, out bool _runWhenSkipped) {
            TextEventEntry _entry = Find(_name);
            _actions = _entry?.Actions;
            _runWhenSkipped = _entry != null && _entry.RunWhenSkipped;
            return _entry != null;
        }

        private TextEventEntry Find(string _name) {
            if (string.IsNullOrEmpty(_name)) return null;
            for (int _i = 0; _i < entries.Count; _i++) {
                TextEventEntry _entry = entries[_i];
                if (_entry != null && string.Equals(_entry.Name, _name, StringComparison.OrdinalIgnoreCase)) return _entry;
            }
            return null;
        }
    }
}
