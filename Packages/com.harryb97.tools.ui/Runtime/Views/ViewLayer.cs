using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

using Logger = HarryTools.LoggerTool.Logger;
using Object = UnityEngine.Object;

namespace HarryTools.UI {
    /// <summary>
    /// Pila de vistas de una capa. La vista de arriba recibe la entrada; las de debajo siguen visibles
    /// salvo que una vista con 'cullViewsBehind' las tape.
    /// </summary>
    [DisallowMultipleComponent]
    public class ViewLayer : MonoBehaviour {
        // ==================== VARIABLES ===================
        #region Events
        public event Action<View> ViewShown;
        public event Action<View> ViewHidden;
        #endregion

        #region Properties
        public UILayerKind Kind => kind;
        public int SortingOrderBase => sortingOrderBase;
        public UIRoot Root { get; internal set; }

        /// <summary> Vistas visibles, de abajo arriba </summary>
        public IReadOnlyList<View> VisibleViews => visible;
        public View Top => visible.Count > 0 ? visible[^1] : null;
        #endregion

        #region Private Variables
        [SerializeField] private UILayerKind kind;
        [SerializeField] private int sortingOrderBase;

        protected readonly List<View> visible = new();
        protected readonly List<View> exiting = new();

        private readonly Dictionary<Type, View> instances = new();
        private readonly Dictionary<Type, View> prefabs = new();
        private View lastTop;
        #endregion

        // ==================== START ====================
        protected virtual void Awake() {
            for (int i = 0; i < transform.childCount; i++) {
                if (transform.GetChild(i).TryGetComponent(out View _view)) Adopt(_view);
            }
        }

        internal void Configure(UILayerKind _kind, int _sortingOrderBase) {
            kind = _kind;
            sortingOrderBase = _sortingOrderBase;
        }

        // ==================== METHODS ====================
        #region Prefabs
        /// <summary> Registra un prefab en esta capa, además de los de las colecciones del UIRoot </summary>
        public void RegisterPrefab(View _prefab) {
            if (_prefab) prefabs[_prefab.GetType()] = _prefab;
        }

        protected virtual bool TryGetPrefab(Type _type, out View _prefab) {
            if (prefabs.TryGetValue(_type, out _prefab) && _prefab) return true;

            foreach (View _candidate in prefabs.Values) {
                if (!_candidate || !_type.IsInstanceOfType(_candidate)) continue;
                _prefab = _candidate;
                return true;
            }

            if (Root) return Root.TryGetPrefab(_type, out _prefab);

            _prefab = null;
            return false;
        }

        /// <summary> Instancia el prefab oculto, si no hay ya una instancia de su tipo </summary>
        internal View Preload(View _prefab) => GetOrCreateInstance(_prefab);
        #endregion

        // =================================================

        #region Show
        /// <summary> Muestra la vista de tipo T: la instancia existente o una nueva a partir de su prefab </summary>
        public virtual T Show<T>() where T : View {
            View _instance = FindInstance(typeof(T));
            if (!_instance && TryGetPrefab(typeof(T), out View _prefab)) _instance = GetOrCreateInstance(_prefab);

            if (!_instance) {
                NotFound(typeof(T));
                return null;
            }

            ShowInstance(_instance);
            return (T)_instance;
        }

        /// <summary> Muestra una instancia de esta capa o, si no lo es, la trata como prefab </summary>
        public virtual T Show<T>(T _viewOrPrefab) where T : View {
            if (!_viewOrPrefab) {
                NotFound(typeof(T));
                return null;
            }

            View _instance = _viewOrPrefab.Layer == this ? _viewOrPrefab : GetOrCreateInstance(_viewOrPrefab);
            ShowInstance(_instance);
            return (T)_instance;
        }

        /// <summary> Como Show, pero instancia en segundo plano si hace falta y espera a que termine la transición de entrada </summary>
        public virtual async UniTask<T> ShowAsync<T>(CancellationToken _ct = default) where T : View {
            View _instance = FindInstance(typeof(T));

            if (!_instance) {
                if (!TryGetPrefab(typeof(T), out View _prefab)) {
                    NotFound(typeof(T));
                    return null;
                }

                _instance = await InstantiateAsync(_prefab, _ct);
            }

            ShowInstance(_instance);
            await _instance.WaitUntilShown(_ct);
            return (T)_instance;
        }

        /// <summary> Instancia la vista de tipo T oculta, para que mostrarla luego no cueste nada </summary>
        public async UniTask PreloadAsync<T>(CancellationToken _ct = default) where T : View {
            if (FindInstance(typeof(T))) return;

            if (!TryGetPrefab(typeof(T), out View _prefab)) {
                NotFound(typeof(T));
                return;
            }

            await InstantiateAsync(_prefab, _ct);
        }

        protected virtual void ShowInstance(View _view) {
            exiting.Remove(_view);
            visible.Remove(_view);
            visible.Add(_view);
            _view.transform.SetAsLastSibling();

            _view.BeginShow();
            Refresh();
            ViewShown?.Invoke(_view);
        }
        #endregion

        // =================================================

        #region Hide
        /// <summary> Oculta la vista visible de tipo T más alta. Devuelve falso si no hay ninguna </summary>
        public virtual bool Hide<T>() where T : View {
            for (int i = visible.Count - 1; i >= 0; i--) {
                if (visible[i] is not T) continue;

                Hide(visible[i]);
                return true;
            }

            return false;
        }

        public virtual void Hide(View _view) {
            if (!_view || _view.Layer != this || !visible.Remove(_view)) return;

            exiting.Add(_view);
            _view.BeginHide();
            Refresh();
        }

        public virtual void HideAll() {
            for (int i = visible.Count - 1; i >= 0; i--) {
                if (i < visible.Count) Hide(visible[i]);
            }
        }
        #endregion

        // =================================================

        #region Queries
        /// <summary> Instancia de tipo T (visible, en caché o precargada), o null </summary>
        public T Get<T>() where T : View => FindInstance(typeof(T)) as T;

        public bool IsVisible<T>() where T : View {
            foreach (View _view in visible) if (_view is T) return true;
            return false;
        }

        /// <summary> Espera a que no quede ninguna vista de tipo T visible ni saliendo en esta capa </summary>
        public virtual UniTask WaitUntilHidden<T>(CancellationToken _ct = default) where T : View =>
            UniTask.WaitUntil(() => !Contains<T>(visible) && !Contains<T>(exiting), cancellationToken: _ct);

        protected static bool Contains<T>(List<View> _views) {
            foreach (View _view in _views) if (_view is T) return true;
            return false;
        }
        #endregion

        // =================================================

        #region Instances
        internal void Adopt(View _view) {
            Register(_view);
            instances[_view.GetType()] = _view;
        }

        /// <summary> Busca primero una instancia del tipo exacto y, si no hay, una de un tipo derivado </summary>
        protected View FindInstance(Type _type) => FindInstance(_type, true) ?? FindInstance(_type, false);

        private View FindInstance(Type _type, bool _exact) {
            bool Matches(View _view) => _view && (_exact ? _view.GetType() == _type : _type.IsInstanceOfType(_view));

            for (int i = visible.Count - 1; i >= 0; i--) if (Matches(visible[i])) return visible[i];
            if (_exact) return instances.TryGetValue(_type, out View _cached) && _cached ? _cached : exiting.Find(Matches);

            foreach (View _instance in instances.Values) if (Matches(_instance)) return _instance;
            return exiting.Find(Matches);
        }

        protected virtual View GetOrCreateInstance(View _prefab) {
            View _existing = FindInstance(_prefab.GetType(), true);
            if (_existing) return _existing;

            View _instance = Instantiate(_prefab, transform, false);
            Register(_instance);
            return _instance;
        }

        protected async UniTask<View> InstantiateAsync(View _prefab, CancellationToken _ct) {
            AsyncInstantiateOperation<View> _operation = Object.InstantiateAsync(_prefab, transform);
            await UniTask.WaitUntil(() => _operation.isDone, cancellationToken: _ct);

            View _instance = _operation.Result[0];
            _instance.transform.SetParent(transform, false);
            Register(_instance);
            return _instance;
        }

        protected void Register(View _view) {
            _view.Initialize();
            _view.Layer = this;
            _view.SetHiddenImmediate();

            if (_view.Lifetime != ViewLifetime.Transient) instances[_view.GetType()] = _view;
        }

        internal void Forget(View _view) {
            visible.Remove(_view);
            exiting.Remove(_view);
            if (instances.TryGetValue(_view.GetType(), out View _cached) && _cached == _view) instances.Remove(_view.GetType());
            if (lastTop == _view) lastTop = null;
            if (this) Refresh();
        }
        #endregion

        // =================================================

        #region View Callbacks
        internal void OnViewEntered(View _view) {
            Refresh();
        }

        internal virtual void OnViewExited(View _view) {
            exiting.Remove(_view);
            ViewHidden?.Invoke(_view);

            if (_view.Lifetime == ViewLifetime.Transient) {
                _view.Layer = null;
                Destroy(_view.gameObject);
            }
        }
        #endregion

        // =================================================

        /// <summary> Orden de dibujado, vistas tapadas, entrada y avisos de primer plano </summary>
        protected void Refresh() {
            int _cullBelow = -1;

            for (int i = visible.Count - 1; i >= 0; i--) {
                if (!visible[i].CullViewsBehind || visible[i].State != ViewState.Visible) continue;
                _cullBelow = i;
                break;
            }

            View _top = Top;

            for (int i = 0; i < visible.Count; i++) {
                View _view = visible[i];
                _view.SetSortingOrder(sortingOrderBase + i);
                _view.SetCulled(i < _cullBelow);
                _view.SetInteractable(_view == _top && _view.State == ViewState.Visible);
            }

            if (_top == lastTop) return;

            View _previous = lastTop;
            lastTop = _top;

            if (_previous && _previous.IsVisible) _previous.NotifyBackground();
            if (_top) _top.NotifyForeground();
        }

        private void NotFound(Type _type) {
            Logger.LogWarning("UI", $"No se ha encontrado ninguna vista de tipo '{_type.Name}'");
        }
    }
}
