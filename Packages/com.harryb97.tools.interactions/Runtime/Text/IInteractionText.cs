using System;

namespace HarryTools.Interactions {
    /// <summary> Texto de una interacción para la UI (por ejemplo, "Abrir") </summary>
    public interface IInteractionText {
        string Value { get; }

        /// <summary> Se lanza cuando el texto cambia (por ejemplo, al cambiar el idioma) </summary>
        event Action Changed;
    }
}
