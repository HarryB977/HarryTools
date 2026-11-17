using System;
using HarryTools.Actions;
using HarryTools.Conditions;
using HarryTools.SerializeReferences;

namespace HarryTools.Dialogue {
    /// <summary> Gives the main dialogue the input that moves past its current line </summary>
    [Serializable, TypeMenu("Dialogue/Advance Dialogue")]
    public sealed class AdvanceDialogueAction : InstantAction {
        // ==================== METHODS ====================
        protected override void Execute(LogicContext _context) {
            if (Dialogue.Main != null) Dialogue.Main.Advance();
        }
    }
}
