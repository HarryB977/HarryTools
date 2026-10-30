using UnityEngine;
using HarryTools.UICanvas;

namespace HarryTools.Popups {
    public abstract class Popup : CustomCanvas<Popup, Popup.PopupParameters> {
        public abstract class PopupParameters : CanvasParameters {}

        // ==================== VARIABLES ===================
        #region Properties
        public bool DoesRespectQueue => respectQueue;
        #endregion
        
        #region Private Variables
        [Header("Popup")]
        [SerializeField] private bool respectQueue = true;
        [SerializeReference, SubclassSelector] private IPopupAnimation popupAnimation;
        #endregion
        
        // ==================== START ====================
        protected virtual void OnEnable() {
            // Register
            PopupManager.Instance.RegisterPopup(this);
        }

        protected virtual void OnDisable() {
            // Register
            PopupManager.Instance.UnregisterPopup(this);
        }
        
        protected virtual void Start() {
            // Escondemos el popup por defecto
            SetActiveState(false);
        }
        
        // ==================== METHODS ====================
        protected override void ChangeVisionState(bool _state) {
            // Mostramos o escondemos el popup dependiendo de la animación que tenga
            if (popupAnimation == null) SetActiveState(_state);
            else {
                if (_state) popupAnimation.ShowAnimation(this);
                else popupAnimation.HideAnimation(this);
            }
        }
    }
}