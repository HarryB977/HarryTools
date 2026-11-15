using System;
using UnityEngine;
using UnityEngine.Events;
using HarryTools.SerializeReferences;

namespace HarryTools.Interactions {
    /// <summary> Lanza un UnityEvent al ejecutar la interacción. Es instantáneo </summary>
    [Serializable, TypeMenu("Unity Event")]
    public class UnityEventResponse : IInteractionResponse {
        // ==================== VARIABLES ===================
        [SerializeField] private UnityEvent onInteract = new();

        // ==================== INTERFACES ===================
        public bool IsRunning => false;

        public void Respond(Interactable _source, IInteractor _interactor) => onInteract?.Invoke();
    }
}
