using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using HarryTools.SerializeReferences;
using UnityEngine;
using UnityEngine.UI;

using Logger = HarryTools.LoggerTool.Logger;

namespace HarryTools.UI {
    /// <summary>
    /// Base de cualquier vista. Puede mostrarse en cualquier capa (Views, Popups u Overlays).
    /// Ocultar desactiva su Canvas, nunca el GameObject.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas), typeof(CanvasGroup), typeof(GraphicRaycaster))]
    public class View : MonoBehaviour {
        // ==================== VARIABLES ===================
        #region Events
        /// <summary> Se lanza al empezar a mostrarse (estado Entering) </summary>
        public event Action<View> Shown;
        /// <summary> Se lanza al terminar de ocultarse (estado Hidden) </summary>
        public event Action<View> Hidden;
        #endregion

        #region Properties
        public Canvas Canvas { get { Initialize(); return canvas; } }
        public CanvasGroup CanvasGroup { get { Initialize(); return canvasGroup; } }
        public RectTransform RectTransform { get { Initialize(); return rectTransform; } }

        public ViewLayer Layer { get; internal set; }
        public ViewState State { get; private set; } = ViewState.Hidden;

        public ViewLifetime Lifetime { get => lifetime; internal set => lifetime = value; }
        public bool CullViewsBehind { get => cullViewsBehind; internal set => cullViewsBehind = value; }
        public bool RespectQueue { get => respectQueue; internal set => respectQueue = value; }
        public bool HideOnBackdropClick { get => hideOnBackdropClick; internal set => hideOnBackdropClick = value; }
        public IViewTransition Transition { get => transition; set => transition = value; }

        public bool IsVisible => State is ViewState.Entering or ViewState.Visible;
        public bool IsTop => Layer && Layer.Top == this;
        #endregion

        #region Private Variables
        [Header("Vista")]
        [Tooltip("Cached: se crea al mostrarse la primera vez y se conserva. Preloaded: se crea al iniciar. Transient: se destruye al ocultarse.")]
        [SerializeField] private ViewLifetime lifetime = ViewLifetime.Cached;
        [Tooltip("Deja de dibujar las vistas de su capa que quedan detrás mientras esta es visible")]
        [SerializeField] private bool cullViewsBehind;
        [SerializeReference, TypeSelector] private IViewTransition transition;

        [Header("Popups")]
        [Tooltip("Si ya hay un popup visible, espera su turno en la cola")]
        [SerializeField] private bool respectQueue = true;
        [Tooltip("Se oculta al hacer clic en el fondo oscurecido")]
        [SerializeField] private bool hideOnBackdropClick = true;

        private Canvas canvas;
        private CanvasGroup canvasGroup;
        private RectTransform rectTransform;
        private CancellationTokenSource transitionCts;
        private bool initialized;
        #endregion

        // ==================== START ====================
        protected virtual void Awake() {
            Initialize();
        }

        protected virtual void OnDestroy() {
            CancelTransition();
            if (Layer) Layer.Forget(this);
        }

        internal void Initialize() {
            if (initialized) return;
            initialized = true;

            canvas = GetComponent<Canvas>();
            canvasGroup = GetComponent<CanvasGroup>();
            rectTransform = (RectTransform)transform;
        }

        // ==================== METHODS ====================
        /// <summary> Muestra esta vista en su capa. Si no tiene capa, en la capa de Views del UIRoot </summary>
        public void Show() {
            if (Layer) Layer.Show(this);
            else if (UI.HasRoot) UI.Views.Show(this);
            else Logger.LogWarning("UI", $"La vista '{name}' no pertenece a ninguna capa y no hay ningún UIRoot activo");
        }

        /// <summary> Oculta esta vista. Se puede asignar al OnClick de un botón </summary>
        public void Hide() {
            if (Layer) Layer.Hide(this);
        }

        /// <summary> Espera a que termine la transición de entrada </summary>
        public UniTask WaitUntilShown(CancellationToken _ct = default) =>
            UniTask.WaitUntil(() => this == null || State == ViewState.Visible, cancellationToken: _ct);

        /// <summary> Espera a que termine de ocultarse </summary>
        public UniTask WaitUntilHidden(CancellationToken _ct = default) =>
            UniTask.WaitUntil(() => this == null || State == ViewState.Hidden, cancellationToken: _ct);

        // =================================================

        #region Overridable
        protected virtual void OnShown() { }
        protected virtual void OnHidden() { }

        /// <summary> Pasa a ser la vista superior de su capa </summary>
        protected virtual void OnBecomeForeground() { }

        /// <summary> Otra vista se ha puesto encima en su capa </summary>
        protected virtual void OnBecomeBackground() { }

        protected virtual UniTask OnEnterTransition(CancellationToken _ct) =>
            transition != null ? transition.Enter(this, _ct) : UniTask.CompletedTask;

        protected virtual UniTask OnExitTransition(CancellationToken _ct) =>
            transition != null ? transition.Exit(this, _ct) : UniTask.CompletedTask;
        #endregion

        // =================================================

        #region Layer API
        internal void SetHiddenImmediate() {
            Initialize();
            CancelTransition();

            State = ViewState.Hidden;
            canvas.enabled = false;
            SetInteractable(false);
        }

        internal void SetInteractable(bool _state) {
            canvasGroup.interactable = _state;
            canvasGroup.blocksRaycasts = _state;
        }

        internal void SetCulled(bool _culled) {
            canvas.enabled = !_culled && State != ViewState.Hidden;
        }

        internal void SetSortingOrder(int _order) {
            canvas.overrideSorting = true;
            canvas.sortingOrder = _order;
        }

        internal void NotifyForeground() => OnBecomeForeground();
        internal void NotifyBackground() => OnBecomeBackground();

        internal void BeginShow() {
            Initialize();
            CancellationToken _ct = RestartTransition();

            State = ViewState.Entering;
            canvas.enabled = true;
            SetInteractable(false);

            Shown?.Invoke(this);
            OnShown();

            RunEnter(_ct).Forget();
        }

        internal void BeginHide() {
            Initialize();
            CancellationToken _ct = RestartTransition();

            State = ViewState.Exiting;
            SetInteractable(false);

            RunExit(_ct).Forget();
        }

        private async UniTaskVoid RunEnter(CancellationToken _ct) {
            try {
                await OnEnterTransition(_ct);
            }
            catch (OperationCanceledException) {
                return;
            }

            if (_ct.IsCancellationRequested || this == null) return;

            State = ViewState.Visible;
            if (Layer) Layer.OnViewEntered(this);
        }

        private async UniTaskVoid RunExit(CancellationToken _ct) {
            try {
                await OnExitTransition(_ct);
            }
            catch (OperationCanceledException) {
                return;
            }

            if (_ct.IsCancellationRequested || this == null) return;

            State = ViewState.Hidden;
            canvas.enabled = false;

            Hidden?.Invoke(this);
            OnHidden();

            if (Layer) Layer.OnViewExited(this);
        }

        private CancellationToken RestartTransition() {
            CancelTransition();
            transitionCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            return transitionCts.Token;
        }

        private void CancelTransition() {
            if (transitionCts == null) return;

            transitionCts.Cancel();
            transitionCts.Dispose();
            transitionCts = null;
        }
        #endregion
    }
}
