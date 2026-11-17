using System;
using HarryTools.Actions;
using HarryTools.Conditions;
using HarryTools.SerializeReferences;

namespace HarryTools.Dialogue {
    /// <summary> Skips the main dialogue up to its next choice or its end </summary>
    [Serializable, TypeMenu("Dialogue/Skip Dialogue")]
    public sealed class SkipDialogueAction : InstantAction {
        // ==================== METHODS ====================
        protected override void Execute(LogicContext _context) {
            if (Dialogue.Main != null) Dialogue.Main.Skip();
        }
    }
}
