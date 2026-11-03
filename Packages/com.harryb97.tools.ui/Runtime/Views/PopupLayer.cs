using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using PrimeTween;
using UnityEngine;
using UnityEngine.UI;

namespace HarryTools.UI {
    /// <summary>
    /// Capa de popups: admite varias instancias del mismo tipo, una cola para los que respetan turno
    /// y un fondo oscurecido que oculta el popup superior al hacer clic.
    /// </summary>
    public class PopupLayer : ViewLayer {
        // ==================== VARIABLES ===================
        #region Properties
        /// <summary> Popups esperando su turno </summary>
        public int QueuedCount => queue.Count;
        public bool IsBackdropVisible { get; private set; }

        internal CanvasGroup Backdrop => backdrop;
        #endregion

        #region Private Variables
        [Header("Backdrop")]
        [SerializeField] private CanvasGroup backdrop;
        [SerializeField] private float backdropFadeDuration = 0.25f;

        private readonly List<View> queue = new();
        private readonly Dictionary<Type, Stack<View>> pool = new();
        private Canvas backdropCanvas;
        private Tween backdropTween;
        #endregion

        // ==================== START ====================
        protected override void Awake() {
            base.Awake();
            SetupBackdrop();
        }

        private void OnDestroy() {
            if (backdropTween.isAlive) backdropTween.Stop();
        }

        internal void SetBackdrop(CanvasGroup _backdrop) {
            backdrop = _backdrop;
            SetupBackdrop();
        }

        private void SetupBackdrop() {
            if (!backdrop) return;

            backdropCanvas = backdrop.GetComponent<Canvas>();
            if (!backdrop.TryGetComponent(out PopupBackdrop _clickHandler)) _clickHandler = backdrop.gameObject.AddComponent<PopupBackdrop>();
            _clickHandler.Layer = this;

            backdrop.alpha = 0f;
            backdrop.blocksRaycasts = false;
            backdrop.interactable = false;
            if (backdropCanvas) backdropCanvas.enabled = false;
        }

        // ==================== METHODS ====================
        #region Show
        /// <summary> Muestra un popup de tipo T (o lo pone en cola). Devuelve la instancia para configurarla con Setup </summary>
        public override T Show<T>() {
            View _instance = TakeReusable(typeof(T));

            if (!_instance) {
                if (!TryGetPrefab(typeof(T), out View _prefab)) {
                    NotFound(typeof(T));
                    return null;
                }

                _instance = CreateInstance(_prefab);
            }

            Route(_instance);
            return (T)_instance;
        }

        public override T Show<T>(T _viewOrPrefab) {
            if (!_viewOrPrefab) {
                NotFound(typeof(T));
                return null;
            }

            View _instance = _viewOrPrefab.Layer == this
                ? _viewOrPrefab
                : TakeReusable(_viewOrPrefab.GetType()) ?? CreateInstance(_viewOrPrefab);

            Route(_instance);
            return (T)_instance;
        }

        public override async UniTask<T> ShowAsync<T>(CancellationToken _ct = default) {
            View _instance = TakeReusable(typeof(T));

            if (!_instance) {
                if (!TryGetPrefab(typeof(T), out View _prefab)) {
                    NotFound(typeof(T));
                    return null;
                }

                _instance = await InstantiateAsync(_prefab, _ct);
            }

            Route(_instance);
            await _instance.WaitUntilShown(_ct);
            return (T)_instance;
        }

        private void Route(View _view) {
            if (visible.Contains(_view)) {
                ShowInstance(_view);
                return;
            }

            if (queue.Contains(_view)) return;

            if (_view.RespectQueue && visible.Exists(_visible => _visible.RespectQueue)) {
                queue.Add(_view);
                _view.IsQueued = true;
                return;
            }

            ShowInstance(_view);
        }

        private void ShowNextQueued() {
            queue.RemoveAll(_queued => !_queued);
            if (queue.Count == 0 || visible.Exists(_visible => _visible.RespectQueue)) return;

            View _next = queue[0];
            queue.RemoveAt(0);
            _next.IsQueued = false;
            ShowInstance(_next);
        }
        #endregion

        // =================================================

        #region Hide
        public override void Hide(View _view) {
            if (_view && queue.Remove(_view)) {
                Release(_view);
                return;
            }

            base.Hide(_view);
        }

        /// <summary> Oculta todos los popups y vacía la cola </summary>
        public override void HideAll() {
            foreach (View _queued in queue.ToArray()) {
                queue.Remove(_queued);
                Release(_queued);
            }

            base.HideAll();
        }

        public override UniTask WaitUntilHidden<T>(CancellationToken _ct = default) =>
            UniTask.WaitUntil(() => !Contains<T>(visible) && !Contains<T>(exiting) && !Contains<T>(queue), cancellationToken: _ct);

        internal override void OnViewExited(View _view) {
            base.OnViewExited(_view);
            if (_view.Lifetime != ViewLifetime.Transient) Pool(_view);
            ShowNextQueued();
        }

        internal override void Forget(View _view) {
            queue.Remove(_view);
            if (pool.TryGetValue(_view.GetType(), out Stack<View> _stack) && _stack.Contains(_view)) {
                var _kept = new List<View>(_stack);
                _kept.Remove(_view);
                _kept.Reverse();
                pool[_view.GetType()] = new Stack<View>(_kept);
            }
            base.Forget(_view);
        }

        internal void OnBackdropClicked() {
            View _top = Top;
            if (_top && _top.HideOnBackdropClick) _top.Hide();
        }
        #endregion

        // =================================================

        #region Instances
        private View CreateInstance(View _prefab) {
            View _instance = Instantiate(_prefab, transform, false);
            Register(_instance);
            return _instance;
        }

        /// <summary> Un popup oculto del tipo exacto que no esté en cola: de la reserva o colocado en escena </summary>
        private View TakeReusable(Type _type) {
            if (pool.TryGetValue(_type, out Stack<View> _stack)) {
                while (_stack.Count > 0) {
                    View _pooled = _stack.Pop();
                    if (IsReusable(_pooled)) return _pooled;
                }
            }

            View _existing = FindInstance(_type, true);
            return IsReusable(_existing) ? _existing : null;
        }

        private bool IsReusable(View _view) =>
            _view && _view.State == ViewState.Hidden && !visible.Contains(_view) && !exiting.Contains(_view) && !queue.Contains(_view);

        /// <summary> Un popup en cola que nunca llegó a mostrarse </summary>
        private void Release(View _view) {
            _view.IsQueued = false;

            if (_view.Lifetime == ViewLifetime.Transient) {
                _view.Layer = null;
                Destroy(_view.gameObject);
                return;
            }

            Pool(_view);
        }

        private void Pool(View _view) {
            if (!pool.TryGetValue(_view.GetType(), out Stack<View> _stack)) pool[_view.GetType()] = _stack = new Stack<View>();
            if (!_stack.Contains(_view)) _stack.Push(_view);
        }
        #endregion

        // =================================================

        #region Backdrop
        protected override void OnRefreshed() {
            bool _show = visible.Count > 0;

            if (backdropCanvas) {
                backdropCanvas.overrideSorting = true;
                backdropCanvas.sortingOrder = _show ? SortingOrderFor(visible.Count - 1) - 1 : SortingOrderBase;
            }

            if (_show == IsBackdropVisible) return;
            IsBackdropVisible = _show;

            if (!backdrop) return;

            backdrop.blocksRaycasts = _show;
            backdrop.interactable = _show;

            if (backdropTween.isAlive) backdropTween.Stop();
            if (_show && backdropCanvas) backdropCanvas.enabled = true;

            backdropTween = Tween.Alpha(backdrop, _show ? 1f : 0f, backdropFadeDuration, useUnscaledTime: true);
            if (!_show) backdropTween.OnComplete(this, _layer => {
                if (!_layer.IsBackdropVisible && _layer.backdropCanvas) _layer.backdropCanvas.enabled = false;
            }, warnIfTargetDestroyed: false);
        }

        /// <summary> Crea el fondo oscurecido por defecto (imagen negra al 60 %) </summary>
        internal static CanvasGroup CreateDefaultBackdrop(Transform _parent) {
            var _rect = new GameObject("Backdrop", typeof(RectTransform)).GetComponent<RectTransform>();
            _rect.SetParent(_parent, false);
            _rect.anchorMin = Vector2.zero;
            _rect.anchorMax = Vector2.one;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;

            _rect.gameObject.AddComponent<Canvas>();
            _rect.gameObject.AddComponent<GraphicRaycaster>();
            _rect.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);
            _rect.gameObject.AddComponent<PopupBackdrop>();
            return _rect.gameObject.AddComponent<CanvasGroup>();
        }
        #endregion
    }
}
