using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace HarryTools.Interactions {
    /// <summary> Objeto con el que se puede interactuar (por ejemplo, el pomo de una puerta). Necesita un collider para que lo detecten </summary>
    [AddComponentMenu("HarryTools/Interactions/Interactable")]
    public class Interactable : MonoBehaviour {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private bool active = true;
        [SerializeField] private List<Interaction> interactions = new();

        [Header("Events")]
        [SerializeField] private UnityEvent onFocusEntered = new();
        [SerializeField] private UnityEvent onFocusExited = new();
        [SerializeField] private UnityEvent onInteracted = new();

        private readonly HashSet<Interaction> subscribed = new();
        #endregion

        #region Properties
        public bool IsActive => active;

        /// <summary> Puede recibir foco: está activo, habilitado y tiene al menos una interacción activa </summary>
        public bool CanBeFocused => this != null && isActiveAndEnabled && active && HasActiveInteraction();

        public IReadOnlyList<Interaction> Interactions => interactions;

        /// <summary> Interacciones activas, en orden </summary>
        public IEnumerable<Interaction> AvailableInteractions {
            get {
                for (int i = 0; i < interactions.Count; i++) if (interactions[i] != null && interactions[i].IsActive) yield return interactions[i];
            }
        }
        #endregion

        #region Events
        public event Action<IInteractor> FocusEntered;
        public event Action<IInteractor> FocusExited;
        public event Action<IInteractor, Interaction> Interacted;

        /// <summary> Cambió el estado activo, la habilitación o alguna interacción (texto o estado) </summary>
        public event Action<Interactable> Changed;
        #endregion

        // ==================== START ===================
        private void Awake() {
            for (int i = 0; i < interactions.Count; i++) Subscribe(interactions[i]);
        }

        private void OnEnable() => Changed?.Invoke(this);

        private void OnDisable() => Changed?.Invoke(this);

        private void OnDestroy() {
            foreach (Interaction _interaction in subscribed) _interaction.Changed -= Event_OnInteractionChanged;
            subscribed.Clear();
        }

        // ==================== METHODS ===================
        public void SetActive(bool _active) {
            if (active == _active) return;
            active = _active;
            Changed?.Invoke(this);
        }

        /// <summary> Primera interacción activa de ese tipo </summary>
        public bool TryGetInteraction(InteractionType _type, out Interaction _interaction) {
            _interaction = null;
            if (_type == null) return false;
            for (int i = 0; i < interactions.Count; i++) {
                Interaction _candidate = interactions[i];
                if (_candidate == null || !_candidate.IsActive || _candidate.Type != _type) continue;
                _interaction = _candidate;
                return true;
            }
            return false;
        }

        /// <summary> Primera interacción de ese tipo, esté activa o no </summary>
        internal bool TryGetInteractionAnyState(InteractionType _type, out Interaction _interaction) {
            _interaction = null;
            if (_type == null) return false;
            for (int i = 0; i < interactions.Count; i++) {
                if (interactions[i] == null || interactions[i].Type != _type) continue;
                _interaction = interactions[i];
                return true;
            }
            return false;
        }

        internal void AddInteraction(Interaction _interaction) {
            if (_interaction == null) return;
            interactions.Add(_interaction);
            Subscribe(_interaction);
            Changed?.Invoke(this);
        }

        internal void EnterFocus(IInteractor _interactor) {
            FocusEntered?.Invoke(_interactor);
            onFocusEntered?.Invoke();
        }

        internal void ExitFocus(IInteractor _interactor) {
            FocusExited?.Invoke(_interactor);
            onFocusExited?.Invoke();
        }

        internal bool Interact(IInteractor _interactor, InteractionType _type, out Interaction _interaction) {
            _interaction = null;
            if (!CanBeFocused || !TryGetInteraction(_type, out Interaction _candidate)) return false;
            if (!_candidate.Execute(this, _interactor)) return false;

            _interaction = _candidate;
            Interacted?.Invoke(_interactor, _candidate);
            onInteracted?.Invoke();
            return true;
        }

        private bool HasActiveInteraction() {
            for (int i = 0; i < interactions.Count; i++) if (interactions[i] != null && interactions[i].IsActive) return true;
            return false;
        }

        private void Subscribe(Interaction _interaction) {
            if (_interaction == null || !subscribed.Add(_interaction)) return;
            _interaction.Changed += Event_OnInteractionChanged;
        }

        // ==================== EVENTS ===================
        private void Event_OnInteractionChanged(Interaction _interaction) => Changed?.Invoke(this);
    }
}
