using System;
using UnityEngine;
using UnityEngine.UI;

using HarryUtils;

namespace HarryTools.UICanvas {
    [RequireComponent(typeof(Canvas), typeof(GraphicRaycaster), typeof(CanvasGroup))]
    public abstract class CustomCanvas<TCanvas, TParams> : MonoBehaviour {
        public abstract class CanvasParameters {
            public Action OnShow;
            public Action OnHide;
        }

        // ==================== VARIABLES ===================
        #region Delegates
        public delegate void ShowHideDelegate(CustomCanvas<TCanvas, TParams> _canvas);

        public ShowHideDelegate OnShow;
        public ShowHideDelegate OnHide;
        #endregion

        #region Properties
        public Canvas Canvas => canvas;
        public CanvasGroup CanvasGroup => canvasGroup;
        public RectTransform Content => content;

        public CustomParentCanvas<TCanvas, TParams> ParentCanvas => parentCanvas;

        public bool IsShowing => canvasGroup.alpha >= 1f;
        #endregion
        
        #region Private Variables
        private RectTransform content;

        private Canvas canvas;
        private CanvasGroup canvasGroup;
        private GraphicRaycaster graphicRaycaster;

        protected TParams canvasParameters;
        protected TCanvas canvasDelegate;
        protected CustomParentCanvas<TCanvas, TParams> parentCanvas;
        #endregion
        
        // ==================== START ====================
        protected virtual void Awake() {
            // Components
            transform.GetChild(0).TryGetComponent(out content);

            TryGetComponent(out canvas);
            TryGetComponent(out canvasGroup);
            TryGetComponent(out graphicRaycaster);

            transform.parent.TryGetComponent(out parentCanvas);
        }
        
        // ==================== METHODS ====================
        public virtual void Show() {
            ChangeVisionState(true);

            OnShow?.Invoke(this);
            (canvasParameters as CanvasParameters)?.OnShow?.Invoke();
        }

        public virtual void Hide() {
            ChangeVisionState(false);

            OnHide?.Invoke(this);
            (canvasParameters as CanvasParameters)?.OnHide?.Invoke();
        }

        public virtual void UpdateParameters(TParams _parameters) {
            canvasParameters = _parameters;
        }

        public virtual void UpdateDelegate(TCanvas _delegate) {
            canvasDelegate = _delegate;
        }

        // =================================================

        protected virtual void ChangeVisionState(bool _state) {
            SetActiveState(_state);
        }

        // =================================================

        public void SetActiveState(bool _state) {
            canvasGroup.SetActive(_state);
            graphicRaycaster.enabled = _state;
        }

        public void SetInteractableState(bool _state) {
            canvasGroup.SetInteractable(_state);
            graphicRaycaster.enabled = _state;
        }
    }
}