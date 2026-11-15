using System;
using UnityEngine;
using HarryTools.SerializeReferences;

namespace HarryTools.Interactions {
    /// <summary> Una forma de interactuar con un Interactable: un tipo, un texto para la UI y lo que ocurre al ejecutarla </summary>
    [Serializable]
    public class Interaction {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private InteractionType type;
        [Tooltip("Text for the UI, e.g. \"Open\". Optional."), SerializeReference, TypeSelector] private IInteractionText text;
        [SerializeField] private bool active = true;
        [Tooltip("Ignores new executions while a response is still running, e.g. a door animation."), SerializeField] private bool ignoreWhileRunning = true;
        [SerializeReference, TypeSelector] private IInteractionResponse[] responses = Array.Empty<IInteractionResponse>();

        private Action<Interaction> changed;
        private IInteractionText subscribedText;
        #endregion

        #region Properties
        public InteractionType Type => type;
        public string Text => text?.Value ?? "";
        public bool IsActive => active;
        public bool IgnoreWhileRunning => ignoreWhileRunning;
        public bool IsRunning {
            get {
                if (responses == null) return false;
                for (int i = 0; i < responses.Length; i++) if (responses[i] != null && responses[i].IsRunning) return true;
                return false;
            }
        }
        #endregion

        #region Events
        /// <summary> Se lanza al cambiar el texto (también cuando el propio texto avisa de un cambio) o el estado activo </summary>
        public event Action<Interaction> Changed {
            add {
                changed += value;
                SubscribeText();
            }
            remove {
                changed -= value;
                if (changed == null) UnsubscribeText();
            }
        }
        #endregion

        // ==================== CONSTRUCTORS ===================
        public Interaction() { }

        internal Interaction(InteractionType _type, IInteractionText _text = null, params IInteractionResponse[] _responses) {
            type = _type;
            text = _text;
            responses = _responses ?? Array.Empty<IInteractionResponse>();
        }

        // ==================== METHODS ===================
        public void SetActive(bool _active) {
            if (active == _active) return;
            active = _active;
            changed?.Invoke(this);
        }

        /// <summary> Sustituye el texto por un texto fijo </summary>
        public void SetText(string _text) => SetText(new FixedText(_text));

        public void SetText(IInteractionText _text) {
            UnsubscribeText();
            text = _text;
            if (changed != null) SubscribeText();
            changed?.Invoke(this);
        }

        /// <summary> Ejecuta todas las respuestas. Devuelve false si está inactiva o si se ignora por seguir en marcha </summary>
        internal bool Execute(Interactable _source, IInteractor _interactor) {
            if (!active) return false;
            if (ignoreWhileRunning && IsRunning) return false;
            if (responses == null) return true;

            for (int i = 0; i < responses.Length; i++) responses[i]?.Respond(_source, _interactor);
            return true;
        }

        private void SubscribeText() {
            if (subscribedText == text) return;
            UnsubscribeText();
            if (text == null) return;
            subscribedText = text;
            subscribedText.Changed += Event_OnTextChanged;
        }

        private void UnsubscribeText() {
            if (subscribedText == null) return;
            subscribedText.Changed -= Event_OnTextChanged;
            subscribedText = null;
        }

        // ==================== EVENTS ===================
        private void Event_OnTextChanged() => changed?.Invoke(this);
    }
}
