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
        /// <summary> Popup esperando su turno en la cola </summary>
        public bool IsQueued { get; internal set; }
        /// <summary> Capa donde se precarga y donde se muestra con view.Show() si no tiene capa </summary>
        public UILayerKind DefaultLayer { get => defaultLayer; internal set => defaultLayer = value; }
        public bool IsTop => Layer && Layer.Top == this;
        #endregion

        #region Private Variables
        [Header("View")]
        [Tooltip("Cached: created the first time it is shown and kept. Preloaded: created on startup. Transient: destroyed when hidden.")]
        [SerializeField] private ViewLifetime lifetime = ViewLifetime.Cached;
        [Tooltip("Layer it is preloaded into (Preloaded) and shown in by Show() when it does not belong to a layer yet.")]
        [SerializeField] private UILayerKind defaultLayer = UILayerKind.Views;
        [Tooltip("Stops drawing the views of its layer behind it while this one is visible.")]
        [SerializeField] private bool cullViewsBehind;
        [SerializeReference, TypeSelector] private IViewTransition transition;

        [Header("Popups")]
        [Tooltip("If a popup is already visible, waits its turn in the queue.")]
        [SerializeField] private bool respectQueue = true;
        [Tooltip("Hides when the dimmed backdrop is clicked.")]
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
        /// <summary> Muestra esta vista en su capa. Si no tiene capa, en su capa por defecto del UIRoot </summary>
        public void Show() {
            if (Layer) Layer.Show(this);
            else if (UI.HasRoot) UI.Root.GetLayer(defaultLayer).Show(this);
            else Logger.LogWarning("UI", $"View '{name}' does not belong to a layer and there is no active UIRoot");
        }

        /// <summary> Oculta esta vista. Se puede asignar al OnClick de un botón </summary>
        public void Hide() {
            if (Layer) Layer.Hide(this);
        }

        /// <summary>
        /// Espera a que termine la transición de entrada. También termina si la vista se oculta
        /// (o sale de la cola) sin llegar a mostrarse, para no esperar para siempre.
        /// </summary>
        public UniTask WaitUntilShown(CancellationToken _ct = default) =>
            UniTask.WaitUntil(() => this == null || State == ViewState.Visible
                || (!IsQueued && State is ViewState.Hidden or ViewState.Exiting), cancellationToken: _ct);

        /// <summary> Espera a que termine de ocultarse. Un popup en cola cuenta como pendiente, no como oculto </summary>
        public UniTask WaitUntilHidden(CancellationToken _ct = default) =>
            UniTask.WaitUntil(() => this == null || (State == ViewState.Hidden && !IsQueued), cancellationToken: _ct);

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

        internal void SetInteractable(bool _state) => SetInput(_state, _state);

        /// <summary> blocksRaycasts evita que los clics atraviesen la vista; interactable activa sus controles </summary>
        internal void SetInput(bool _blocksRaycasts, bool _interactable) {
            canvasGroup.blocksRaycasts = _blocksRaycasts;
            canvasGroup.interactable = _interactable;
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
            canvasGroup.interactable = false;

            if (Layer) Layer.RaiseShown(this);
            Shown?.Invoke(this);
            OnShown();

            // Un manejador de Shown puede haberla ocultado ya
            if (_ct.IsCancellationRequested || State != ViewState.Entering) return;
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
            catch (Exception _exception) {
                Logger.LogError("UI", $"Enter transition of '{name}' failed: {_exception}");
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
            catch (Exception _exception) {
                Logger.LogError("UI", $"Exit transition of '{name}' failed: {_exception}");
            }

            if (_ct.IsCancellationRequested || this == null) return;

            State = ViewState.Hidden;
            canvas.enabled = false;

            Hidden?.Invoke(this);
            OnHidden();

            // Un manejador de Hidden puede haberla vuelto a mostrar
            if (_ct.IsCancellationRequested || this == null || State != ViewState.Hidden) return;
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
