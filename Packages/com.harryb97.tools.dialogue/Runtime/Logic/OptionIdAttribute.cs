using UnityEngine;

namespace HarryTools.Dialogue {
    /// <summary> Marks a string field that holds a choice option id of the dialogue held by a sibling field. Drawn as a list of the options of that dialogue </summary>
    public sealed class OptionIdAttribute : PropertyAttribute {
        // ==================== VARIABLES ===================
        #region Public Variables
        /// <summary> Name of the sibling field that holds the <see cref="DialogueAsset"/> </summary>
        public string DialogueField { get; }
        #endregion

        // ==================== START ====================
        public OptionIdAttribute(string _dialogueField) {
            DialogueField = _dialogueField;
        }
    }
}
