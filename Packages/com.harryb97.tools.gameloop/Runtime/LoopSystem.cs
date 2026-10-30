using System.Collections.Generic;
using UnityEngine;

namespace HarryTools.GameLoop {
    public static class LoopSystem {
        // ==================== VARIABLES ===================
        #region Delegates
        public delegate void LoopDelegate();
        #endregion
        
        #region Public Variables
        public static Dictionary<string, ILoopable> Loops { get; private set; }
        #endregion

        // ==================== METHODS ====================
        public static void Initialize() {
            // Collections
            Loops = new();
            
            // Manager
            GameObject _loopManager = new("Loop Manager");
            _loopManager.AddComponent<LoopManager>();

            Object.DontDestroyOnLoad(_loopManager);
        }
        
        public static void AddLoop(ILoopable _loop) {
            if (Loops.ContainsKey(_loop.LoopID)) {
                return;
            }

            Loops.Add(_loop.LoopID, _loop);
        }
        
        public static void RemoveLoop(string _loop) {
            if (!Loops.ContainsKey(_loop)) {
                return;
            }

            Loops.Remove(_loop);
        }
    }
}