using System;
using UnityEngine;

namespace HarryTools.Flags
{
    /// <summary>
    /// Serialized reference to a flag by guid. The label is only used in warnings.
    /// </summary>
    [Serializable]
    public sealed class FlagRef
    {
        // ==================== VARIABLES ====================

        [SerializeField] private string guid;
        [SerializeField] private string label;

        [NonSerialized] internal bool warned;

        // ==================== CONSTRUCTORS ====================

        public FlagRef() { }

        internal FlagRef(string _guid, string _label)
        {
            guid = _guid;
            label = _label;
        }

        // ==================== PROPERTIES ====================

        public string Guid => guid;

        /// <summary>Scope and name at the time the flag was assigned.</summary>
        public string Label => label;

        public bool IsAssigned => !string.IsNullOrEmpty(guid);

        /// <summary>Current value, read without a reader.</summary>
        public bool IsSet => Flags.IsSet(this);

        // ==================== METHODS ====================

        /// <summary>Current value; the reader is reported to the debug history.</summary>
        public bool Read(UnityEngine.Object _reader) => Flags.IsSet(this, _reader);

        public void Set(UnityEngine.Object _source = null) => Flags.Set(this, _source);

        public void Clear(UnityEngine.Object _source = null) => Flags.Clear(this, _source);

        public void Toggle(UnityEngine.Object _source = null) => Flags.Toggle(this, _source);

        public void SetValue(bool _value, UnityEngine.Object _source = null) => Flags.SetValue(this, _value, _source);

        public void Subscribe(Action<FlagChange> _callback) => Flags.Subscribe(this, _callback);

        public void Unsubscribe(Action<FlagChange> _callback) => Flags.Unsubscribe(this, _callback);
    }
}
