using System;
using UnityEngine;

using HarryTools.Conditions;
using HarryTools.SerializeReferences;

namespace HarryTools.Actions.Variants {
    /// <summary> Cierra la aplicación (en el editor, sale del modo Play) </summary>
    [Serializable, TypeMenu("Application/Quit")]
    public class QuitAction : InstantAction {
        // ==================== METHODS ====================
        protected override void Execute(LogicContext _context) {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExitPlaymode();
#else
            Application.Quit();
#endif
        }
    }
}
