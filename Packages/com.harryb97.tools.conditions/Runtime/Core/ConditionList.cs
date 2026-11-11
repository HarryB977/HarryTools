using System;
using UnityEngine;

using HarryTools.SerializeReferences;

namespace HarryTools.Conditions {
    /// <summary> List of conditions ready to use as a field in any component </summary>
    /// <example> [SerializeField] private ConditionList conditions; ... if (conditions.Check(LogicContext.From(this))) { } </example>
    [Serializable]
    public class ConditionList {
        public enum EvaluationMode {
            [Tooltip("Every condition passes (AND)")] All,
            [Tooltip("At least one condition passes (OR)")] Any
        }

        // ==================== VARIABLES ===================
        #region Properties
        public EvaluationMode Mode => mode;
        public ICondition[] Conditions => conditions;
        public bool IsEmpty => ConditionUtils.IsNullOrEmpty(conditions);
        #endregion

        #region Private Variables
        [SerializeField] private EvaluationMode mode = EvaluationMode.All;
        [SerializeReference, TypeSelector] private ICondition[] conditions = Array.Empty<ICondition>();
        #endregion

        // ==================== START ====================
        public ConditionList() { }

        public ConditionList(EvaluationMode _mode, params ICondition[] _conditions) {
            mode = _mode;
            conditions = _conditions;
        }

        // ==================== METHODS ====================
        /// <summary> An empty list always passes </summary>
        public bool Check(LogicContext _context) {
            return mode == EvaluationMode.All ? conditions.CheckAll(_context) : conditions.CheckAny(_context, true);
        }
    }
}
