using System;
using UnityEngine;
using UnityEngine.Events;

namespace HarryTools.Interactions {
    /// <summary> Quien interactúa (por ejemplo, el jugador). Lleva el foco y ejecuta interacciones por tipo. La detección la hace quien lo usa: llama a SetFocus e Interact </summary>
    [AddComponentMenu("HarryTools/Interactions/Interactor")]
    public class Interactor : MonoBehaviour, IInteractor {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private UnityEvent<Interactable> onFocusChanged = new();

        private Interactable focus;
        #endregion

        #region Properties
        public GameObject GameObject => gameObject;
        public Interactable Focus => focus;
        #endregion

        #region Events
        /// <summary> (anterior, actual). Cualquiera de los dos puede ser null </summary>
        public event Action<Interactable, Interactable> FocusChanged;

        /// <summary> El foco sigue siendo el mismo, pero cambió su texto o sus interacciones </summary>
        public event Action<Interactable> FocusUpdated;

        public event Action<Interactable, Interaction> Interacted;
        #endregion

        // ==================== START ===================
        private void OnDisable() => SetFocus(null);

        // ==================== METHODS ===================
        /// <summary> Cambia el foco. Con null, o con un objeto que no puede recibir foco, lo suelta </summary>
        public void SetFocus(Interactable _candidate) {
            if (_candidate != null && !_candidate.CanBeFocused) _candidate = null;
            if (ReferenceEquals(_candidate, focus)) return;

            Interactable _previous = focus;
            if (!ReferenceEquals(_previous, null)) _previous.Changed -= Event_OnFocusChanged;
            focus = _candidate;
            if (!ReferenceEquals(focus, null)) focus.Changed += Event_OnFocusChanged;

            if (!ReferenceEquals(_previous, null)) _previous.ExitFocus(this);
            if (!ReferenceEquals(focus, null)) focus.EnterFocus(this);

            FocusChanged?.Invoke(_previous, focus);
            onFocusChanged?.Invoke(focus);
        }

        /// <summary> Ejecuta sobre el foco la primera interacción activa de ese tipo. Devuelve false si no se ejecutó nada </summary>
        public bool Interact(InteractionType _type) {
            if (focus == null || _type == null) return false;
            if (!focus.Interact(this, _type, out Interaction _interaction)) return false;

            Interacted?.Invoke(focus, _interaction);
            return true;
        }

        // ==================== EVENTS ===================
        private void Event_OnFocusChanged(Interactable _interactable) {
            if (!_interactable.CanBeFocused) SetFocus(null);
            else FocusUpdated?.Invoke(_interactable);
        }
    }
}
