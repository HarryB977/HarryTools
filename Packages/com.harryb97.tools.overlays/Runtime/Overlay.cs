using HarryTools.UICanvas;
using UnityEngine;

namespace HarryTools.Overlays {
    public abstract class Overlay : CustomCanvas<Overlay, Overlay.OverlayParameters> {
        public abstract class OverlayParameters : CanvasParameters { }

        // ==================== VARIABLES ===================
        #region Private Variables
        [Header("Overlay")]
        [SerializeReference, SubclassSelector] private IOverlayAnimation overlayAnimation;
        #endregion

        // ==================== START ====================
        protected virtual void OnEnable() {
            // Register
            OverlayManager.Instance.RegisterOverlay(this);
        }

        protected virtual void OnDisable() {
            // Register
            OverlayManager.Instance.UnregisterOverlay(this);
        }

        protected virtual void Start() {
            // Escondemos el overlay al principio
            SetActiveState(false);
        }

        // ==================== METHODS ====================
        protected override void ChangeVisionState(bool _state) {
            // Mostramos o escondemos el overlay dependiendo de la animación que tenga
            if (overlayAnimation == null) SetActiveState(_state);
            else {
                if (_state) overlayAnimation.ShowAnimation(this);
                else overlayAnimation.HideAnimation(this);
            }
        }

        /// <summary> Cambia la animación del Overlay </summary>
        /// <param name="_viewAnimation"></param>
        public void ChangeAnimation(IOverlayAnimation _viewAnimation) {
            overlayAnimation = _viewAnimation;
        }
    }
}