using System;
using HarryTools.Actions;
using HarryTools.Conditions;
using HarryTools.SerializeReferences;
using UnityEngine;

namespace HarryTools.Dialogue {
    /// <summary> Clears what the memory holds about a dialogue: its play count and the options chosen </summary>
    [Serializable, TypeMenu("Dialogue/Forget Dialogue")]
    public sealed class ForgetDialogueAction : InstantAction {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private DialogueAsset dialogue;
        #endregion

        // ==================== START ====================
        public ForgetDialogueAction() { }

        public ForgetDialogueAction(DialogueAsset _dialogue) {
            dialogue = _dialogue;
        }

        // ==================== METHODS ====================
        protected override void Execute(LogicContext _context) => DialogueMemory.Forget(dialogue);
    }
}
