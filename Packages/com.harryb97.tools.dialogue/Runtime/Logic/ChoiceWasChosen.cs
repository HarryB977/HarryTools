using System;
using HarryTools.Conditions;
using HarryTools.SerializeReferences;
using UnityEngine;

namespace HarryTools.Dialogue {
    /// <summary> Passes when the option was chosen in the dialogue at some point, as the memory remembers it </summary>
    [Serializable, TypeMenu("Dialogue/Choice Was Chosen")]
    public sealed class ChoiceWasChosen : Condition {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private DialogueAsset dialogue;
        [OptionId(nameof(dialogue)), SerializeField] private string optionId = "";
        #endregion

        // ==================== START ====================
        public ChoiceWasChosen() { }

        public ChoiceWasChosen(DialogueAsset _dialogue, string _optionId) {
            dialogue = _dialogue;
            optionId = _optionId;
        }

        // ==================== METHODS ====================
        protected override bool Evaluate(LogicContext _context) => DialogueMemory.WasChosen(dialogue, optionId);
    }
}
