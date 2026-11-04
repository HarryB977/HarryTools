using System.Collections.Generic;
using UnityEngine;

namespace HarryTools.GameLoop {
    public static class LoopSystem {
        // ==================== VARIABLES ===================
        #region Delegates
        public delegate void LoopDelegate();
        #endregion

        #region Public Variables
        public static Dictionary<string, ILoopable> Loops { get; private set; } = new();
        public static bool IsInitialized => s_manager != null;
        #endregion

        #region Private Variables
        private static GameObject s_manager;
        private static int s_iterationDepth;
        private static readonly List<PendingChange> s_pending = new();

        private readonly struct PendingChange {
            public readonly bool Add;
            public readonly ILoopable Loop;
            public readonly string LoopID;

            public PendingChange(bool _add, ILoopable _loop, string _loopId) {
                Add = _add;
                Loop = _loop;
                LoopID = _loopId;
            }
        }
        #endregion

        // ==================== METHODS ====================
        /// <summary>
        /// Creates the Loop Manager if it does not exist. Safe to call more than once: it never clears the registered loops.
        /// </summary>
        public static void Initialize() {
            EnsureInitialized();
        }

        public static bool HasLoop(string _loopId) {
            EnsureInitialized();
            return Loops.ContainsKey(_loopId);
        }

        public static bool TryGetLoop(string _loopId, out ILoopable _loop) {
            EnsureInitialized();
            return Loops.TryGetValue(_loopId, out _loop);
        }

        public static void AddLoop(ILoopable _loop) {
            EnsureInitialized();

            if (s_iterationDepth > 0) {
                s_pending.Add(new PendingChange(true, _loop, _loop.LoopID));
                return;
            }

            if (Loops.ContainsKey(_loop.LoopID)) {
                return;
            }

            Loops.Add(_loop.LoopID, _loop);
        }

        public static void RemoveLoop(string _loop) {
            EnsureInitialized();

            if (s_iterationDepth > 0) {
                s_pending.Add(new PendingChange(false, null, _loop));
                return;
            }

            Loops.Remove(_loop);
        }

        // =================================================
        internal static void Reset() {
            if (s_manager != null) {
                if (Application.isPlaying) Object.Destroy(s_manager);
                else Object.DestroyImmediate(s_manager);
            }

            s_manager = null;
            s_iterationDepth = 0;
            s_pending.Clear();
            Loops = new();
        }

        internal static void BeginIteration() {
            s_iterationDepth++;
        }

        internal static void EndIteration() {
            s_iterationDepth--;
            if (s_iterationDepth > 0 || s_pending.Count == 0) return;

            // Applied in the order they were requested
            PendingChange[] _changes = s_pending.ToArray();
            s_pending.Clear();

            foreach (PendingChange _change in _changes) {
                if (_change.Add) {
                    Loops.TryAdd(_change.LoopID, _change.Loop);
                } else {
                    Loops.Remove(_change.LoopID);
                }
            }
        }

        internal static void OnManagerDestroyed(GameObject _manager) {
            if (s_manager == _manager) s_manager = null;
        }

        private static void EnsureInitialized() {
            if (s_manager != null) return;

            s_manager = new GameObject("Loop Manager");
            s_manager.AddComponent<LoopManager>();

            // Only valid in Play mode; in Edit mode (tests, tooling) it would throw
            if (Application.isPlaying) Object.DontDestroyOnLoad(s_manager);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() {
            Reset();
        }
    }
}
