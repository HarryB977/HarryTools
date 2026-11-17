using System;
using UnityEngine;
using UnityEngine.Localization;

namespace HarryTools.Dialogue {
    /// <summary> Shows the choice offered by the main dialogue. The main <see cref="LineContainer"/> calls <see cref="Show"/> and <see cref="Hide"/>; buttons, navigation and the timer bar belong to subclasses, which read <see cref="Prompt"/> and report back through its Select, MoveSelection, Confirm and Choose </summary>
    [AddComponentMenu("HarryTools/Dialogue/Choice View")]
    public class ChoiceView : MonoBehaviour {
        // ==================== VARIABLES ===================
        #region Public Variables
        /// <summary> The choice shown, or null while hidden </summary>
        public ChoicePrompt Prompt { get; private set; }
        #endregion

        // ==================== METHODS ====================
        #region Public Methods
        /// <summary> Activates the view, follows the prompt's selection and refreshes </summary>
        public virtual void Show(ChoicePrompt _prompt) {
            if (_prompt == null) return;
            Unsubscribe();
            Prompt = _prompt;
            Prompt.Changed += OnPromptChanged;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            Refresh();
        }

        /// <summary> Stops following the prompt and deactivates the view </summary>
        public virtual void Hide() {
            Unsubscribe();
            Prompt = null;
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }

        /// <summary> Text of the option in the current locale, or an empty string </summary>
        public string GetOptionText(int _index) {
            ChoiceOptionState _state = StateAt(_index);
            return _state != null ? Resolve(_state.Option.Text) : string.Empty;
        }

        /// <summary> Why the option cannot be chosen, in the current locale. Empty when it is available or has no reason </summary>
        public string GetLockedReason(int _index) {
            ChoiceOptionState _state = StateAt(_index);
            if (_state == null || _state.Available) return string.Empty;
            return Resolve(_state.Option.LockedReason);
        }
        #endregion

        #region Protected Methods
        /// <summary> Called when the view is shown and whenever the selection changes. Draws <see cref="Prompt"/> </summary>
        protected virtual void Refresh() { }
        #endregion

        // ==================== UNITY ====================
        #region Unity
        protected virtual void OnDestroy() {
            Unsubscribe();
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        private void OnPromptChanged() {
            Refresh();
        }

        private void Unsubscribe() {
            if (Prompt != null) Prompt.Changed -= OnPromptChanged;
        }

        private ChoiceOptionState StateAt(int _index) {
            if (Prompt == null || _index < 0 || _index >= Prompt.Options.Count) return null;
            return Prompt.Options[_index];
        }

        private static string Resolve(LocalizedString _string) {
            if (_string == null || _string.IsEmpty) return string.Empty;
            try {
                return _string.GetLocalizedString() ?? string.Empty;
            } catch (Exception) {
                return string.Empty;
            }
        }
        #endregion
    }
}
