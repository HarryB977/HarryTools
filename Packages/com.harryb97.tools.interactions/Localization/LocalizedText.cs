using System;
using UnityEngine;
using UnityEngine.Localization;
using HarryTools.SerializeReferences;

namespace HarryTools.Interactions.Localization {
    /// <summary> Texto traducido con Unity Localization. Avisa de los cambios de idioma </summary>
    [Serializable, TypeMenu("Localized Text")]
    public class LocalizedText : IInteractionText {
        // ==================== VARIABLES ===================
        [SerializeField] private LocalizedString text = new();

        private Action changed;
        private bool listening;

        // ==================== CONSTRUCTORS ===================
        public LocalizedText() { }

        internal LocalizedText(LocalizedString _text) {
            text = _text ?? new LocalizedString();
        }

        // ==================== INTERFACES ===================
        public string Value => text == null || text.IsEmpty ? "" : text.GetLocalizedString() ?? "";

        public event Action Changed {
            add {
                changed += value;
                if (changed != null) StartListening();
            }
            remove {
                changed -= value;
                if (changed == null) StopListening();
            }
        }

        // ==================== METHODS ===================
        private void StartListening() {
            if (listening || text == null || text.IsEmpty) return;
            text.StringChanged += Event_OnStringChanged;
            listening = true;
        }

        private void StopListening() {
            if (!listening) return;
            text.StringChanged -= Event_OnStringChanged;
            listening = false;
        }

        // ==================== EVENTS ===================
        private void Event_OnStringChanged(string _value) => changed?.Invoke();
    }
}
