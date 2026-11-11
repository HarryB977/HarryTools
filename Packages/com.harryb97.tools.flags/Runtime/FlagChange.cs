namespace HarryTools.Flags
{
    /// <summary>Why a flag changed value.</summary>
    public enum FlagChangeReason
    {
        /// <summary>Changed during play (code, listener, window).</summary>
        Live,
        /// <summary>Changed because a snapshot was restored.</summary>
        Restore,
        /// <summary>Changed because the state was reset to initial values.</summary>
        Reset
    }

    /// <summary>Describes one flag value change.</summary>
    public readonly struct FlagChange
    {
        public FlagChange(string _guid, FlagDefinition _flag, bool _oldValue, bool _newValue, FlagChangeReason _reason, UnityEngine.Object _source)
        {
            Guid = _guid;
            Flag = _flag;
            OldValue = _oldValue;
            NewValue = _newValue;
            Reason = _reason;
            Source = _source;
        }

        public string Guid { get; }
        public FlagDefinition Flag { get; }
        public bool OldValue { get; }
        public bool NewValue { get; }
        public FlagChangeReason Reason { get; }

        /// <summary>Object that asked for the change, if it said so.</summary>
        public UnityEngine.Object Source { get; }
    }

    /// <summary>Describes one flag read. Only raised inside the Editor.</summary>
    public readonly struct FlagRead
    {
        public FlagRead(string _guid, bool _value, UnityEngine.Object _reader)
        {
            Guid = _guid;
            Value = _value;
            Reader = _reader;
        }

        public string Guid { get; }
        public bool Value { get; }
        public UnityEngine.Object Reader { get; }
    }
}
