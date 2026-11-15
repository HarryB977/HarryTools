using System;
using UnityEngine;
using HarryTools.SerializeReferences;

namespace HarryTools.Interactions {
    /// <summary> Texto fijo, sin traducir </summary>
    [Serializable, TypeMenu("Fixed Text")]
    public class FixedText : IInteractionText {
        // ==================== VARIABLES ===================
        [SerializeField] private string text = "";

        // ==================== CONSTRUCTORS ===================
        public FixedText() { }

        public FixedText(string _text) {
            text = _text ?? "";
        }

        // ==================== INTERFACES ===================
        public string Value => text ?? "";

        // Un texto fijo no cambia por sí solo: para cambiarlo se sustituye con Interaction.SetText
        public event Action Changed { add { } remove { } }
    }
}
