using System;
using HarryTools.TextEffects;
using UnityEngine.Localization;

namespace HarryTools.Dialogue {
    public enum HistoryEntryKind {
        Line,
        Choice
    }

    /// <summary> One line shown or option chosen, kept by <see cref="DialogueHistory"/> </summary>
    public sealed class HistoryEntry {
        // ==================== VARIABLES ===================
        #region Private Variables
        // Set for lines so the text goes through the same resolution as the one shown
        private readonly LineNode sourceLine;
        #endregion

        #region Public Variables
        public HistoryEntryKind Kind { get; }
        public DialogueCharacter Character { get; }
        public LocalizedString Text { get; }
        public DialogueAsset Dialogue { get; }
        #endregion

        // ==================== CONSTRUCTORS ====================
        internal HistoryEntry(DialogueAsset _dialogue, LineNode _line) {
            Kind = HistoryEntryKind.Line;
            Dialogue = _dialogue;
            Character = _line.Character;
            Text = _line.Text;
            sourceLine = _line;
        }

        internal HistoryEntry(DialogueAsset _dialogue, ChoiceOption _option) {
            Kind = HistoryEntryKind.Choice;
            Dialogue = _dialogue;
            Text = _option.Text;
        }

        // ==================== METHODS ====================
        #region Public Methods
        /// <summary> Text in the current language without the text effects tags. TextMeshPro tags are kept. Empty when it cannot be resolved </summary>
        public string GetText() {
            try {
                string _text = sourceLine != null ? ResolveLine() : Resolve(Text);
                if (string.IsNullOrEmpty(_text)) return string.Empty;
                return TextParser.Parse(_text, TextEffectsSettings.Current).TmpText ?? string.Empty;
            } catch (Exception) {
                return string.Empty;
            }
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        private string ResolveLine() => HarryTools.Dialogue.Dialogue.ResolveLineText(sourceLine);

        private static string Resolve(LocalizedString _text) {
            if (_text == null || _text.IsEmpty) return null;
            return _text.GetLocalizedString();
        }
        #endregion
    }
}
