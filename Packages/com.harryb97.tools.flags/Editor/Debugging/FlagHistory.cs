using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HarryTools.Flags.Editor
{
    /// <summary>
    /// In-memory log of the flag changes and reads of the current Play session. Newest entry first, capped at
    /// <see cref="Capacity"/>. Emptied every time Play starts. Never creates assets.
    /// </summary>
    [InitializeOnLoad]
    internal static class FlagHistory
    {
        // ==================== VARIABLES ====================

        public const int Capacity = 500;

        private const string WindowSourceName = "Flags window";
        private const string CodeSource = "Code";

        private static readonly List<FlagHistoryEntry> entries = new List<FlagHistoryEntry>(Capacity + 1);
        private static FlagsWindowSource windowSource;

        // ==================== EVENTS ====================

        /// <summary>Raised after an entry was added or updated, and after <see cref="Clear"/>.</summary>
        public static event Action Updated;

        // ==================== PROPERTIES ====================

        /// <summary>Every entry, most recent first.</summary>
        public static IReadOnlyList<FlagHistoryEntry> All => entries;

        /// <summary>Source the Flags window passes for its writes, so they are told apart from code writes without a source.</summary>
        public static UnityEngine.Object WindowSource
        {
            get
            {
                if (windowSource == null)
                {
                    windowSource = ScriptableObject.CreateInstance<FlagsWindowSource>();
                    windowSource.name = WindowSourceName;
                    windowSource.hideFlags = HideFlags.HideAndDontSave;
                }
                return windowSource;
            }
        }

        // ==================== SETUP ====================

        static FlagHistory()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= DestroyWindowSource;
            AssemblyReloadEvents.beforeAssemblyReload += DestroyWindowSource;
            Resubscribe();
        }

        /// <summary>
        /// Runs after the domain reload reset cleared the runtime handlers and before any Awake, so nothing of the first frame is missed.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResubscribeOnPlayStart() => Resubscribe();

        // ==================== PUBLIC API ====================

        public static IEnumerable<FlagHistoryEntry> For(string _flagGuid)
        {
            for (int _i = 0; _i < entries.Count; _i++)
                if (entries[_i].FlagGuid == _flagGuid) yield return entries[_i];
        }

        /// <summary>Time of the latest change (not read) of the flag, or -1 when it has none.</summary>
        public static double LastChangeTime(string _flagGuid)
        {
            for (int _i = 0; _i < entries.Count; _i++)
                if (!entries[_i].IsRead && entries[_i].FlagGuid == _flagGuid) return entries[_i].Time;
            return -1;
        }

        // ==================== INTERNAL ====================

        internal static void Record(FlagChange _change)
        {
            string _sourceName = _change.Source != null ? _change.Source.name
                : _change.Reason == FlagChangeReason.Live ? CodeSource : _change.Reason.ToString();

            Add(new FlagHistoryEntry
            {
                Time = Time.realtimeSinceStartupAsDouble,
                FlagGuid = _change.Guid,
                OldValue = _change.OldValue,
                NewValue = _change.NewValue,
                Reason = _change.Reason,
                SourceName = _sourceName,
                SourceInstanceId = _change.Source != null ? _change.Source.GetInstanceID() : 0
            });
        }

        internal static void Record(FlagRead _read)
        {
            string _sourceName = _read.Reader != null ? _read.Reader.name : CodeSource;
            int _sourceId = _read.Reader != null ? _read.Reader.GetInstanceID() : 0;

            if (entries.Count > 0)
            {
                FlagHistoryEntry _last = entries[0];
                if (_last.IsRead && _last.FlagGuid == _read.Guid && _last.NewValue == _read.Value && _last.SourceInstanceId == _sourceId && _last.SourceName == _sourceName)
                {
                    _last.Count++;
                    _last.Time = Time.realtimeSinceStartupAsDouble;
                    Updated?.Invoke();
                    return;
                }
            }

            Add(new FlagHistoryEntry
            {
                Time = Time.realtimeSinceStartupAsDouble,
                FlagGuid = _read.Guid,
                IsRead = true,
                OldValue = _read.Value,
                NewValue = _read.Value,
                Reason = FlagChangeReason.Live,
                SourceName = _sourceName,
                SourceInstanceId = _sourceId
            });
        }

        internal static void Clear()
        {
            entries.Clear();
            Updated?.Invoke();
        }

        /// <summary>
        /// Subscribes to the runtime events, first removing any earlier subscription. Needed on entering Play because the
        /// runtime clears its handlers when domain reload is disabled.
        /// </summary>
        internal static void Resubscribe()
        {
            Flags.Changed -= Record;
            Flags.Read -= Record;
            Flags.Changed += Record;
            Flags.Read += Record;
        }

        // ==================== PRIVATE ====================

        private static void Add(FlagHistoryEntry _entry)
        {
            entries.Insert(0, _entry);
            if (entries.Count > Capacity) entries.RemoveAt(entries.Count - 1);
            Updated?.Invoke();
        }

        // The marker would outlive the static that holds it across a domain reload.
        private static void DestroyWindowSource()
        {
            if (windowSource != null) UnityEngine.Object.DestroyImmediate(windowSource);
            windowSource = null;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange _state)
        {
            if (_state == PlayModeStateChange.ExitingEditMode) Clear();
            else if (_state == PlayModeStateChange.EnteredPlayMode) Resubscribe();
        }
    }
}
