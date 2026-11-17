using System;
using HarryTools.Conditions;
using HarryTools.SerializeReferences;
using UnityEngine;

namespace HarryTools.Dialogue {
    /// <summary> Compares the number of times the dialogue was started with a value </summary>
    [Serializable, TypeMenu("Dialogue/Play Count")]
    public sealed class DialoguePlayCount : Condition {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private DialogueAsset dialogue;
        [SerializeField] private Comparison comparison = Comparison.GreaterOrEqual;
        [SerializeField, Min(0)] private int count = 1;
        #endregion

        // ==================== START ====================
        public DialoguePlayCount() { }

        public DialoguePlayCount(DialogueAsset _dialogue, Comparison _comparison, int _count) {
            dialogue = _dialogue;
            comparison = _comparison;
            count = _count;
        }

        // ==================== METHODS ====================
        protected override bool Evaluate(LogicContext _context) => comparison.Evaluate(DialogueMemory.PlayCount(dialogue), count);
    }
}
