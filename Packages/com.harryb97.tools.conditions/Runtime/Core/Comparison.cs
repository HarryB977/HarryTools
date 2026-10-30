using UnityEngine;

namespace HarryTools.Conditions {
    public enum Comparison {
        [InspectorName("==")] Equal,
        [InspectorName("!=")] NotEqual,
        [InspectorName(">")] Greater,
        [InspectorName(">=")] GreaterOrEqual,
        [InspectorName("<")] Less,
        [InspectorName("<=")] LessOrEqual
    }

    public static class ComparisonUtils {
        // ==================== METHODS ====================
        public static bool Evaluate(this Comparison _comparison, float _a, float _b) {
            return _comparison switch {
                Comparison.Equal => Mathf.Approximately(_a, _b),
                Comparison.NotEqual => !Mathf.Approximately(_a, _b),
                Comparison.Greater => _a > _b,
                Comparison.GreaterOrEqual => _a >= _b,
                Comparison.Less => _a < _b,
                Comparison.LessOrEqual => _a <= _b,
                _ => false
            };
        }

        public static bool Evaluate(this Comparison _comparison, int _a, int _b) {
            return _comparison switch {
                Comparison.Equal => _a == _b,
                Comparison.NotEqual => _a != _b,
                Comparison.Greater => _a > _b,
                Comparison.GreaterOrEqual => _a >= _b,
                Comparison.Less => _a < _b,
                Comparison.LessOrEqual => _a <= _b,
                _ => false
            };
        }
    }
}
