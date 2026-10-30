using System;
using UnityEngine;

using HarryTools.Conditions;
using HarryTools.SerializeReferences;

namespace HarryTools.Actions.Variants {
    [Serializable, TypeMenu("Application/Set Time Scale")]
    public class SetTimeScaleAction : InstantAction {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField, Min(0)] private float timeScale = 1f;
        #endregion

        // ==================== METHODS ====================
        protected override void Execute(LogicContext _context) => Time.timeScale = timeScale;
    }
}
