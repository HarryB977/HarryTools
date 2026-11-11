using System.Collections.Generic;

namespace HarryTools.Conditions {
    public static class ConditionUtils {
        // ==================== METHODS ====================
        /// <summary> Every condition passes. Null entries are ignored and an empty list returns true </summary>
        public static bool CheckAll(this IReadOnlyList<ICondition> _conditions, LogicContext _context) {
            if (_conditions == null) return true;

            for (int i = 0; i < _conditions.Count; i++) {
                if (_conditions[i] != null && !_conditions[i].Check(_context)) return false;
            }

            return true;
        }

        /// <summary> At least one condition passes. Null entries are ignored </summary>
        /// <param name="_resultIfEmpty"> Result when no condition is assigned </param>
        public static bool CheckAny(this IReadOnlyList<ICondition> _conditions, LogicContext _context, bool _resultIfEmpty = false) {
            if (IsNullOrEmpty(_conditions)) return _resultIfEmpty;

            for (int i = 0; i < _conditions.Count; i++) {
                if (_conditions[i] != null && _conditions[i].Check(_context)) return true;
            }

            return false;
        }

        public static bool IsNullOrEmpty(IReadOnlyList<ICondition> _conditions) {
            if (_conditions == null) return true;

            for (int i = 0; i < _conditions.Count; i++) {
                if (_conditions[i] != null) return false;
            }

            return true;
        }
    }
}
