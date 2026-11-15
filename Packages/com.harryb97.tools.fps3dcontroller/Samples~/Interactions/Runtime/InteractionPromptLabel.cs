using System.Text;
using UnityEngine;

using HarryTools.Interactions;

namespace HarryTools.FPS3DController.Samples.Interactions {
    /// <summary> Muestra en pantalla las interacciones disponibles del foco, por ejemplo "LMB: Open · RMB: Lock". Solo para el ejemplo: cada juego pinta su propia UI </summary>
    [RequireComponent(typeof(Interactor), typeof(RaycastInteractor))]
    public class InteractionPromptLabel : MonoBehaviour {
        // ==================== VARIABLES ===================
        #region Private Variables
        private Interactor interactor;
        private RaycastInteractor raycastInteractor;
        private string prompt = "";
        private GUIStyle style;
        #endregion

        // ==================== START ====================
        private void Awake() {
            TryGetComponent(out interactor);
            TryGetComponent(out raycastInteractor);
        }

        private void OnEnable() {
            interactor.FocusChanged += Event_OnFocusChanged;
            interactor.FocusUpdated += Event_OnFocusUpdated;
        }

        private void OnDisable() {
            interactor.FocusChanged -= Event_OnFocusChanged;
            interactor.FocusUpdated -= Event_OnFocusUpdated;
        }

        private void OnGUI() {
            style ??= new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 20 };

            GUI.Label(new Rect(Screen.width / 2f - 10f, Screen.height / 2f - 10f, 20f, 20f), "+", style);
            if (prompt.Length > 0) GUI.Label(new Rect(0f, Screen.height * 0.8f, Screen.width, 40f), prompt, style);
        }

        // ==================== EVENTS ====================
        private void Event_OnFocusChanged(Interactable _previous, Interactable _current) => Rebuild(_current);

        private void Event_OnFocusUpdated(Interactable _focus) => Rebuild(_focus);

        // ==================== METHODS ====================
        private void Rebuild(Interactable _focus) {
            if (_focus == null) {
                prompt = "";
                return;
            }

            StringBuilder _builder = new();
            foreach (Interaction _interaction in _focus.AvailableInteractions) {
                string _label = LabelFor(_interaction.Type);
                if (_label == null) continue;
                if (_builder.Length > 0) _builder.Append("  ·  ");
                _builder.Append(_label).Append(": ").Append(_interaction.Text);
            }
            prompt = _builder.ToString();
        }

        private string LabelFor(InteractionType _type) {
            foreach (RaycastInteractor.Binding _binding in raycastInteractor.Bindings) if (_binding.type == _type) return _binding.label;
            return null;
        }
    }
}
