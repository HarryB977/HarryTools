using UnityEngine;

using HarryTools.Interactions;

namespace HarryTools.FPS3DController.Samples.Interactions {
    /// <summary> Puerta de ejemplo: gira sobre su bisagra y se puede bloquear. Sus métodos se llaman desde las interacciones del Interactable </summary>
    public class SampleDoor : MonoBehaviour {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private Transform hinge;
        [SerializeField] private float openAngle = 90f;
        [SerializeField, Min(0f)] private float speed = 4f;

        [Header("Lock text")]
        [Tooltip("Updates the text of this interaction to \"Lock\" / \"Unlock\"."), SerializeField] private Interactable interactable;
        [SerializeField] private InteractionType lockType;

        private float closedAngle;
        #endregion

        #region Properties
        public bool IsOpen { get; private set; }
        public bool IsLocked { get; private set; }
        #endregion

        // ==================== START ====================
        private void Awake() {
            if (hinge != null) closedAngle = hinge.localEulerAngles.y;
        }

        private void Update() {
            if (hinge == null) return;

            float _target = closedAngle + (IsOpen ? openAngle : 0f);
            Vector3 _euler = hinge.localEulerAngles;
            _euler.y = Mathf.LerpAngle(_euler.y, _target, 1f - Mathf.Exp(-speed * Time.deltaTime));
            hinge.localEulerAngles = _euler;
        }

        // ==================== METHODS ====================
        public void ToggleOpen() {
            if (IsLocked) {
                Debug.Log("Locked");
                return;
            }

            IsOpen = !IsOpen;
        }

        public void ToggleLock() {
            IsLocked = !IsLocked;
            if (interactable != null && interactable.TryGetInteraction(lockType, out Interaction _interaction)) _interaction.SetText(IsLocked ? "Unlock" : "Lock");
        }
    }
}
