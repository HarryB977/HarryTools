using System;
using System.Collections.Generic;
using UnityEngine;

using HarryTools.Interactions;

namespace HarryTools.FPS3DController.Samples.Interactions {
    /// <summary> Detecta Interactables con un raycast desde el centro de la cámara y ejecuta sus interacciones con las teclas configuradas </summary>
    [RequireComponent(typeof(Interactor))]
    public class RaycastInteractor : MonoBehaviour {
        [Serializable]
        public class Binding {
            public KeyCode key;
            [Tooltip("Short name shown in the prompt, e.g. \"LMB\".")] public string label;
            public InteractionType type;
        }

        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private Camera rayCamera;
        [SerializeField, Min(0f)] private float maxDistance = 2.5f;
        [SerializeField] private LayerMask layers = ~0;
        [SerializeField] private QueryTriggerInteraction triggers = QueryTriggerInteraction.Ignore;
        [SerializeField] private List<Binding> bindings = new() {
            new Binding { key = KeyCode.Mouse0, label = "LMB" },
            new Binding { key = KeyCode.Mouse1, label = "RMB" }
        };

        private Interactor interactor;
        #endregion

        #region Properties
        public IReadOnlyList<Binding> Bindings => bindings;
        #endregion

        // ==================== START ====================
        private void Awake() {
            TryGetComponent(out interactor);
        }

        private void Update() {
            interactor.SetFocus(FindTarget());

            for (int i = 0; i < bindings.Count; i++) {
                Binding _binding = bindings[i];
                if (_binding.type != null && Input.GetKeyDown(_binding.key)) interactor.Interact(_binding.type);
            }
        }

        // ==================== METHODS ====================
        private Interactable FindTarget() {
            if (rayCamera == null) return null;

            Transform _origin = rayCamera.transform;
            if (!Physics.Raycast(_origin.position, _origin.forward, out RaycastHit _hit, maxDistance, layers, triggers)) return null;
            return _hit.collider.GetComponentInParent<Interactable>();
        }
    }
}
