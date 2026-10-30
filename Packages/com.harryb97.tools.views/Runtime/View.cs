using UnityEngine;
using HarryTools.UICanvas;

namespace HarryTools.Views {
    public abstract class View : CustomCanvas<View, View.ViewParameters> {
        public abstract class ViewParameters : CanvasParameters {}

        // ==================== VARIABLES ===================
        #region Private Variables
        [Header("View")]
        [SerializeField] private bool startHiding = true;
        [SerializeReference, SubclassSelector] private IViewAnimation viewAnimation;
        #endregion
        
        // ==================== START ====================
        protected virtual void OnEnable() {
            // Register
            ViewManager.Instance.RegisterView(this);
        }

        protected virtual void OnDisable() {
            // Register
            ViewManager.Instance.UnregisterView(this);
        }

        protected virtual void Start() {
            // Escondemos o mostramos la View al principio
            if (startHiding) SetActiveState(false);
            else Show();
        }
        
        // ==================== METHODS ====================
        protected override void ChangeVisionState(bool _state) {
            // Mostramos o escondemos la view dependiendo de la animación que tenga
            if (viewAnimation == null) SetActiveState(_state);
            else {
                if (_state) viewAnimation.ShowAnimation(this);
                else viewAnimation.HideAnimation(this);
            }
        }

        /// <summary> Cambia la animación de la View </summary>
        /// <param name="_viewAnimation"></param>
        public void ChangeAnimation(IViewAnimation _viewAnimation) {
            viewAnimation = _viewAnimation;
        }
    }
}