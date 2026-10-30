using System;
using UnityEngine;

using HarryTools.Conditions;
using HarryTools.SerializeReferences;

namespace HarryTools.Actions.Variants {
    [Serializable, TypeMenu("Debug/Log")]
    public class LogAction : InstantAction {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private LogType type = LogType.Log;
        [SerializeField, TextArea] private string message;
        #endregion

        // ==================== METHODS ====================
        protected override void Execute(LogicContext _context) {
            Debug.unityLogger.Log(type, (object)message, _context.Self);
        }
    }
}
