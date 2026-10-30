using System;
using UnityEngine;

using HarryTools.SerializeReferences;

namespace HarryTools.Conditions {
    /// <summary> Lista de condiciones lista para usar como campo en cualquier componente </summary>
    /// <example> [SerializeField] private ConditionList conditions; ... if (conditions.Check(LogicContext.From(this))) { } </example>
    [Serializable]
    public class ConditionList {
        public enum EvaluationMode {
            [Tooltip("Se cumplen todas (AND)")] All,
            [Tooltip("Se cumple al menos una (OR)")] Any
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
        /// <summary> Una lista vacía siempre se cumple </summary>
        public bool Check(LogicContext _context) {
            return mode == EvaluationMode.All ? conditions.CheckAll(_context) : conditions.CheckAny(_context, true);
        }
    }
}
