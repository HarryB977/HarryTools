using UnityEngine;
using UnityEngine.UI;

using HarryUtils;

namespace HarryTools.UICanvas {
    [RequireComponent(typeof(Canvas), typeof(GraphicRaycaster))]
    public abstract class CustomParentCanvas<TCanvas, TParams> : MonoBehaviour {
        // ==================== VARIABLES ===================
        #region Public Variables
        public bool IsShowingBackground { get; protected set; }
        public CanvasScaler CanvasScaler => canvasScaler;
        #endregion
        
        #region Private Variables
        [Header("Parent Canvas")]
        [SerializeField] protected CanvasGroup background;
        [SerializeField] protected bool showBackgroundOnStart;

        protected GraphicRaycaster graphicRaycaster;
        protected CanvasScaler canvasScaler;
        protected Image backgroundImage;
        #endregion
        
        // ==================== START ====================
        private void Awake() {
            // Components
            TryGetComponent(out graphicRaycaster);
            TryGetComponent(out canvasScaler);
            
            background.TryGetComponent(out backgroundImage);

            // Initialization
            IsShowingBackground = !showBackgroundOnStart;
            
            if (showBackgroundOnStart) ShowBackground();
            else HideBackground();
        }
        
        // ==================== METHODS ====================
        public virtual void ChangeBackgroundColor(Color _color) {
            backgroundImage.color = _color;
        }

        public virtual void ShowBackground() {
            if (IsShowingBackground) return;
            IsShowingBackground = true;
            
            background.SetActive(true);
            graphicRaycaster.enabled = true;
        }

        public virtual void HideBackground() {
            if (!IsShowingBackground) return;
            IsShowingBackground = false;
            
            background.SetActive(false);
            graphicRaycaster.enabled = false;
        }
    }
}