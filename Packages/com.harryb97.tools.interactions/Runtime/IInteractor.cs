using UnityEngine;

namespace HarryTools.Interactions {
    /// <summary> Quien interactúa (por ejemplo, el jugador) </summary>
    public interface IInteractor {
        GameObject GameObject { get; }
        Interactable Focus { get; }
    }
}
