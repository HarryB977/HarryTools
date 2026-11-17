using System;
using HarryTools.Conditions;
using HarryTools.SerializeReferences;

namespace HarryTools.Dialogue {
    /// <summary> Passes while a main dialogue is running </summary>
    [Serializable, TypeMenu("Dialogue/Is Main Dialogue Running")]
    public sealed class IsMainDialogueRunning : Condition {
        // ==================== METHODS ====================
        protected override bool Evaluate(LogicContext _context) => Dialogue.Main != null;
    }
}
