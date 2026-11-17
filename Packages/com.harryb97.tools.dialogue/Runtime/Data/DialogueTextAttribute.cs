using UnityEngine;

namespace HarryTools.Dialogue {
    /// <summary> Marks a LocalizedString of a dialogue as text written in the source locale. Its key is the id of the owning node or option plus <see cref="KeySuffix"/> </summary>
    public sealed class DialogueTextAttribute : PropertyAttribute {
        // ==================== VARIABLES ===================
        #region Public Variables
        /// <summary> Appended to the owner id to form the table key. Empty for the main text of the owner </summary>
        public string KeySuffix { get; }
        #endregion

        // ==================== CONSTRUCTOR ====================
        #region Constructor
        public DialogueTextAttribute(string _keySuffix = "") {
            KeySuffix = _keySuffix ?? "";
        }
        #endregion
    }
}
