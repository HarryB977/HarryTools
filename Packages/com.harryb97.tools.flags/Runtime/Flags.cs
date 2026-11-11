using System;
using System.Collections.Generic;
using HarryTools.DomainReload;
using UnityEngine;
using Logger = HarryTools.LoggerTool.Logger;

namespace HarryTools.Flags
{
    /// <summary>
    /// Runtime state of every flag. A flag reads as its changed value, or its initial value if nobody changed it.
    /// </summary>
    public static class Flags
    {
        // ==================== VARIABLES ====================

        private const string LogCategory = "Flags";
        private const string BoolType = "bool";

        [ClearOnReload] private static Dictionary<string, bool> changed = new Dictionary<string, bool>();
        [ClearOnReload] private static Dictionary<string, List<Action<FlagChange>>> subscribers = new Dictionary<string, List<Action<FlagChange>>>();
        [ClearOnReload] private static FlagDatabase database;
        [ClearOnReload] private static bool databaseIsFallback;
        [ClearOnReload] private static Action<FlagChange> changedHandlers;
        [ClearOnReload] private static Action restoredHandlers;
#if UNITY_EDITOR
        [ClearOnReload] private static Action<FlagRead> readHandlers;
#endif

        // ==================== EVENTS ====================

        /// <summary>Raised for every flag whose value changes.</summary>
        public static event Action<FlagChange> Changed
        {
            add => changedHandlers += value;
            remove => changedHandlers -= value;
        }

        /// <summary>Raised once after Restore or ResetAll finished.</summary>
        public static event Action Restored
        {
            add => restoredHandlers += value;
            remove => restoredHandlers -= value;
        }

#if UNITY_EDITOR
        /// <summary>Raised on every read. Editor only, so builds pay nothing.</summary>
        internal static event Action<FlagRead> Read
        {
            add => readHandlers += value;
            remove => readHandlers -= value;
        }
#endif

        // ==================== PUBLIC API ====================

        public static bool IsSet(FlagRef _flag, UnityEngine.Object _reader = null)
        {
            if (_flag == null || !_flag.IsAssigned) return false;
            if (!TryResolve(_flag, false, out _)) return false;

            bool _value = GetValue(_flag.Guid);
#if UNITY_EDITOR
            readHandlers?.Invoke(new FlagRead(_flag.Guid, _value, _reader));
#endif
            return _value;
        }

        public static void Set(FlagRef _flag, UnityEngine.Object _source = null) => SetValue(_flag, true, _source);

        public static void Clear(FlagRef _flag, UnityEngine.Object _source = null) => SetValue(_flag, false, _source);

        public static void Toggle(FlagRef _flag, UnityEngine.Object _source = null)
        {
            if (!TryResolve(_flag, true, out FlagDefinition _definition)) return;
            Write(_definition, !GetValue(_definition.Guid), FlagChangeReason.Live, _source);
        }

        public static void SetValue(FlagRef _flag, bool _value, UnityEngine.Object _source = null)
        {
            if (!TryResolve(_flag, true, out FlagDefinition _definition)) return;
            Write(_definition, _value, FlagChangeReason.Live, _source);
        }

        public static void Subscribe(FlagRef _flag, Action<FlagChange> _callback)
        {
            if (_flag == null || !_flag.IsAssigned || _callback == null) return;
            if (!subscribers.TryGetValue(_flag.Guid, out List<Action<FlagChange>> _list))
            {
                _list = new List<Action<FlagChange>>();
                subscribers[_flag.Guid] = _list;
            }
            _list.Add(_callback);
        }

        public static void Unsubscribe(FlagRef _flag, Action<FlagChange> _callback)
        {
            if (_flag == null || !_flag.IsAssigned || _callback == null) return;
            if (!subscribers.TryGetValue(_flag.Guid, out List<Action<FlagChange>> _list)) return;
            _list.Remove(_callback);
            if (_list.Count == 0) subscribers.Remove(_flag.Guid);
        }

        // ==================== SNAPSHOT ====================

        /// <summary>Saves the flags changed since the last reset.</summary>
        public static FlagSnapshot Capture()
        {
            FlagSnapshot _snapshot = new FlagSnapshot();
            foreach (KeyValuePair<string, bool> _pair in changed)
            {
                _snapshot.entries.Add(new FlagSnapshotEntry { guid = _pair.Key, type = BoolType, value = _pair.Value ? "true" : "false" });
            }
            return _snapshot;
        }

        /// <summary>Replaces the whole state with the snapshot. A null snapshot resets everything.</summary>
        public static void Restore(FlagSnapshot _snapshot)
        {
            Dictionary<string, bool> _next = new Dictionary<string, bool>();
            if (_snapshot != null && _snapshot.entries != null)
            {
                foreach (FlagSnapshotEntry _entry in _snapshot.entries)
                {
                    if (_entry == null) continue;
                    if (!Database.TryGet(_entry.guid, out _))
                    {
                        Logger.LogWarning(LogCategory, $"Snapshot entry {_entry.guid} ignored: no flag with that guid.");
                        continue;
                    }
                    if (_entry.type != BoolType)
                    {
                        Logger.LogWarning(LogCategory, $"Snapshot entry {_entry.guid} ignored: unknown type '{_entry.type}'.");
                        continue;
                    }
                    if (!bool.TryParse(_entry.value, out bool _parsed))
                    {
                        Logger.LogWarning(LogCategory, $"Snapshot entry {_entry.guid} ignored: invalid value '{_entry.value}'.");
                        continue;
                    }
                    _next[_entry.guid] = _parsed;
                }
            }
            Replace(_next, FlagChangeReason.Restore);
        }

        /// <summary>New game: empties the state so every flag returns to its initial value.</summary>
        public static void ResetAll() => Replace(new Dictionary<string, bool>(), FlagChangeReason.Reset);

        // ==================== INTERNAL ====================

        internal static FlagDatabase Database
        {
            get
            {
                if (database == null)
                {
                    database = Resources.Load<FlagDatabase>(FlagDatabase.ResourcesName);
                    databaseIsFallback = false;
                    if (database == null)
                    {
                        database = ScriptableObject.CreateInstance<FlagDatabase>();
                        database.hideFlags = HideFlags.HideAndDontSave;
                        databaseIsFallback = true;
                        Logger.LogWarning(LogCategory, "No FlagDatabase found in a Resources folder. All flags read as false.");
                    }
                }
                return database;
            }
        }

        internal static bool GetValue(string _guid)
        {
            if (changed.TryGetValue(_guid, out bool _value)) return _value;
            return Database.TryGet(_guid, out FlagDefinition _flag) && _flag.InitialValue;
        }

        internal static bool HasOverride(string _guid) => changed.ContainsKey(_guid);

        internal static void UseDatabaseForTests(FlagDatabase _database)
        {
            database = _database;
            databaseIsFallback = false;
        }

        internal static void ClearForTests()
        {
            changed.Clear();
            subscribers.Clear();
            changedHandlers = null;
            restoredHandlers = null;
#if UNITY_EDITOR
            readHandlers = null;
#endif
            if (databaseIsFallback && database != null) UnityEngine.Object.DestroyImmediate(database);
            database = null;
            databaseIsFallback = false;
        }

        // ==================== PRIVATE ====================

        private static bool TryResolve(FlagRef _flag, bool _isWrite, out FlagDefinition _definition)
        {
            _definition = null;
            if (_flag == null) return false;

            if (!_flag.IsAssigned)
            {
                if (_isWrite) WarnOnce(_flag, "Write ignored: the FlagRef is empty.");
                return false;
            }
            if (!Database.TryGet(_flag.Guid, out _definition))
            {
                string _name = string.IsNullOrEmpty(_flag.Label) ? _flag.Guid : _flag.Label;
                WarnOnce(_flag, $"Flag '{_name}' no longer exists. It reads as false and writes are ignored.");
                return false;
            }
            return true;
        }

        private static void WarnOnce(FlagRef _flag, string _message)
        {
            if (_flag.warned) return;
            _flag.warned = true;
            Logger.LogWarning(LogCategory, _message);
        }

        private static void Write(FlagDefinition _definition, bool _value, FlagChangeReason _reason, UnityEngine.Object _source)
        {
            bool _old = GetValue(_definition.Guid);
            if (_old == _value) return;

            changed[_definition.Guid] = _value;
            Notify(new FlagChange(_definition.Guid, _definition, _old, _value, _reason, _source));
        }

        private static void Replace(Dictionary<string, bool> _next, FlagChangeReason _reason)
        {
            Dictionary<string, bool> _previous = changed;
            HashSet<string> _guids = new HashSet<string>(_previous.Keys);
            _guids.UnionWith(_next.Keys);

            List<FlagChange> _changes = new List<FlagChange>();
            foreach (string _guid in _guids)
            {
                if (!Database.TryGet(_guid, out FlagDefinition _flag)) continue;
                bool _old = _previous.TryGetValue(_guid, out bool _oldOverride) ? _oldOverride : _flag.InitialValue;
                bool _new = _next.TryGetValue(_guid, out bool _newOverride) ? _newOverride : _flag.InitialValue;
                if (_old != _new) _changes.Add(new FlagChange(_guid, _flag, _old, _new, _reason, null));
            }

            changed = _next;
            foreach (FlagChange _change in _changes) Notify(_change);
            InvokeSafely(restoredHandlers);
        }

        private static void Notify(FlagChange _change)
        {
            if (changedHandlers != null)
            {
                foreach (Action<FlagChange> _handler in changedHandlers.GetInvocationList()) InvokeSafely(_handler, _change);
            }
            if (!subscribers.TryGetValue(_change.Guid, out List<Action<FlagChange>> _list)) return;

            foreach (Action<FlagChange> _callback in _list.ToArray()) InvokeSafely(_callback, _change);
        }

        private static void InvokeSafely(Action<FlagChange> _callback, FlagChange _change)
        {
            try { _callback(_change); }
            catch (Exception _exception) { Logger.LogError(LogCategory, $"A flag change handler threw: {_exception}"); }
        }

        private static void InvokeSafely(Action _callback)
        {
            if (_callback == null) return;
            foreach (Action _handler in _callback.GetInvocationList())
            {
                try { _handler(); }
                catch (Exception _exception) { Logger.LogError(LogCategory, $"A Restored handler threw: {_exception}"); }
            }
        }
    }
}
