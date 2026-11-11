using System;
using System.Collections.Generic;
using UnityEditor;

namespace HarryTools.SceneRefs.Editor
{
    /// <summary>
    /// Set of strings mirrored to <see cref="SessionState"/>, so queued editor work survives domain reloads and Play.
    /// The in-memory copy is loaded on first use after a reload.
    /// </summary>
    internal sealed class SessionStringSet
    {
        // ==================== VARIABLES ====================

        private const char Separator = '\n';

        private readonly string key;
        private HashSet<string> items;

        // ==================== CONSTRUCTORS ====================

        public SessionStringSet(string _key) => key = _key;

        // ==================== PROPERTIES ====================

        public int Count => Items.Count;

        private HashSet<string> Items
        {
            get
            {
                if (items != null) return items;
                items = new HashSet<string>(StringComparer.Ordinal);
                string _stored = SessionState.GetString(key, string.Empty);
                if (_stored.Length > 0)
                    foreach (string _item in _stored.Split(Separator))
                        if (_item.Length > 0) items.Add(_item);
                return items;
            }
        }

        // ==================== PUBLIC API ====================

        public bool Add(string _item)
        {
            if (string.IsNullOrEmpty(_item) || !Items.Add(_item)) return false;
            Save();
            return true;
        }

        public bool Remove(string _item)
        {
            if (string.IsNullOrEmpty(_item) || !Items.Remove(_item)) return false;
            Save();
            return true;
        }

        public bool Contains(string _item) => !string.IsNullOrEmpty(_item) && Items.Contains(_item);

        /// <summary>Returns every item and empties the set.</summary>
        public List<string> TakeAll()
        {
            List<string> _all = new List<string>(Items);
            Items.Clear();
            Save();
            return _all;
        }

        /// <summary>Drops the in-memory copy, as a domain reload does. Tests only.</summary>
        internal void ForgetInMemory() => items = null;

        // ==================== PRIVATE ====================

        private void Save()
        {
            if (items.Count == 0) SessionState.EraseString(key);
            else SessionState.SetString(key, string.Join(Separator.ToString(), items));
        }
    }
}
