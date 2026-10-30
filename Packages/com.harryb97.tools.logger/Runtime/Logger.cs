using UnityEngine;

namespace HarryTools.LoggerTool {
    public static class Logger {
        // ==================== METHODS ====================
        public static void Initialize() {
    #if DEBUG
            Debug.unityLogger.logEnabled = true;
    #else
            Debug.unityLogger.logEnabled = false;
    #endif
        }
        
        #region Log
        public static void Log(string _category, string _message) {
            Console.Log(_category, _message);
        }
        #endregion

        #region Warning
        public static void LogWarning(string _category, string _message) {
            Console.LogWarning(_category, _message);
        }
        #endregion

        #region Error
        public static void LogError(string _category, string _message) {
            Console.LogError(_category, _message);
        }
        #endregion
    }
}