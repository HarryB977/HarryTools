using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using Logger = HarryTools.LoggerTool.Logger;

namespace HarryTools.UI {
    /// <summary>
    /// Raíz única de la interfaz: contiene las capas Views, Popups y Overlays, las colecciones de prefabs
    /// y la paleta de colores. Solo puede haber uno activo a la vez.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Canvas))]
    public class UIRoot : MonoBehaviour, IPaletteProvider {
        public const int ViewsSortingOrder = 0;
        public const int PopupsSortingOrder = 1000;
        public const int OverlaysSortingOrder = 2000;

        // ==================== VARIABLES ===================
        #region Events
        public event Action onColorChange;
        #endregion

        #region Properties
        public ViewLayer Views => views;
        public PopupLayer Popups => popups;
        public OverlayLayer Overlays => overlays;

        public ColorPalette palette {
            get => colorPalette;
            set {
                if (colorPalette == value) return;

                colorPalette = value;
                SubscribeToPalette();
                onColorChange?.Invoke();
            }
        }
        #endregion

        #region Private Variables
        [Header("Prefabs")]
        [SerializeField] private List<ViewCollection> collections = new();
        [SerializeField] private View showOnStart;

        [Header("Theme")]
        [SerializeField] private ColorPalette colorPalette;

        [Header("Layers")]
        [SerializeField] private ViewLayer views;
        [SerializeField] private PopupLayer popups;
        [SerializeField] private OverlayLayer overlays;

        [Header("Options")]
        [Tooltip("Keeps the UIRoot when loading another scene (if you don't use MultiScene).")]
        [SerializeField] private bool dontDestroyOnLoad;

        private readonly Dictionary<Type, View> registeredPrefabs = new();
        private ColorPalette subscribedPalette;
        private bool registered;
        #endregion

        // ==================== START ====================
        private void Awake() {
            if (UI.HasRoot && UI.Root != this) {
                Logger.LogWarning("UI", $"A UIRoot is already active; ignoring '{name}'.");
                gameObject.SetActive(false);
                return;
            }

            registered = true;
            UI.Register(this);

            foreach (ViewLayer _layer in Layers()) {
                _layer.Root = this;
                _layer.ViewShown += UI.RaiseShown;
                _layer.ViewHidden += UI.RaiseHidden;
            }

            PreloadViews();
            SubscribeToPalette();

            if (dontDestroyOnLoad) {
                if (transform.parent) transform.SetParent(null, true);
                DontDestroyOnLoad(gameObject);
            }
        }

        private void Start() {
            if (registered && showOnStart) views.Show(showOnStart);
        }

        private void OnDestroy() {
            UnsubscribeFromPalette();
            if (!registered) return;

            foreach (ViewLayer _layer in Layers()) {
                _layer.ViewShown -= UI.RaiseShown;
                _layer.ViewHidden -= UI.RaiseHidden;
            }

            UI.Unregister(this);
        }

        private void OnValidate() {
            if (!Application.isPlaying) return;

            SubscribeToPalette();
            onColorChange?.Invoke();
        }

        // ==================== METHODS ====================
        #region Layers & Prefabs
        public ViewLayer GetLayer(UILayerKind _kind) => _kind switch {
            UILayerKind.Popups => popups,
            UILayerKind.Overlays => overlays,
            _ => views
        };

        /// <summary> Registra un prefab además de los de las colecciones </summary>
        public void RegisterPrefab(View _prefab) {
            if (_prefab) registeredPrefabs[_prefab.GetType()] = _prefab;
        }

        public bool TryGetPrefab<T>(out T _prefab) where T : View {
            bool _found = TryGetPrefab(typeof(T), out View _view);
            _prefab = _view as T;
            return _found;
        }

        /// <summary> Busca primero el tipo exacto y después tipos derivados, en los registrados y luego en las colecciones </summary>
        public bool TryGetPrefab(Type _type, out View _prefab) {
            return TryFind(_type, true, out _prefab) || TryFind(_type, false, out _prefab);
        }

        private bool TryFind(Type _type, bool _exact, out View _prefab) {
            bool Matches(View _view) => _view && (_exact ? _view.GetType() == _type : _type.IsInstanceOfType(_view));

            foreach (View _view in registeredPrefabs.Values) {
                if (!Matches(_view)) continue;
                _prefab = _view;
                return true;
            }

            foreach (ViewCollection _collection in collections) {
                if (!_collection || _collection.views == null) continue;

                foreach (View _view in _collection.views) {
                    if (!Matches(_view)) continue;
                    _prefab = _view;
                    return true;
                }
            }

            _prefab = null;
            return false;
        }

        private IEnumerable<ViewLayer> Layers() {
            if (views) yield return views;
            if (popups) yield return popups;
            if (overlays) yield return overlays;
        }

        private void PreloadViews() {
            foreach (ViewCollection _collection in collections) {
                if (!_collection || _collection.views == null) continue;

                foreach (View _prefab in _collection.views) {
                    if (_prefab && _prefab.Lifetime == ViewLifetime.Preloaded) GetLayer(_prefab.DefaultLayer).Preload(_prefab);
                }
            }
        }
        #endregion

        // =================================================

        #region Palette
        private void SubscribeToPalette() {
            if (subscribedPalette == colorPalette) return;

            UnsubscribeFromPalette();
            subscribedPalette = colorPalette;
            if (subscribedPalette) subscribedPalette.onChange += OnPaletteChanged;
        }

        private void UnsubscribeFromPalette() {
            if (!subscribedPalette) return;

            subscribedPalette.onChange -= OnPaletteChanged;
            subscribedPalette = null;
        }

        private void OnPaletteChanged() => onColorChange?.Invoke();
        #endregion

        // =================================================

        #region Factory
        /// <summary> Crea un UIRoot completo: Canvas a pantalla completa, las tres capas y un EventSystem si no hay ninguno </summary>
        public static UIRoot Create(string _name = "UIRoot", params ViewCollection[] _collections) {
            var _go = new GameObject(_name, typeof(RectTransform));
            _go.SetActive(false);

            var _canvas = _go.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var _scaler = _go.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.referenceResolution = new Vector2(1920, 1080);
            _scaler.matchWidthOrHeight = 0.5f;

            _go.AddComponent<GraphicRaycaster>();

            var _root = _go.AddComponent<UIRoot>();
            _root.collections = new List<ViewCollection>(_collections ?? Array.Empty<ViewCollection>());
            _root.views = CreateLayer<ViewLayer>(_go.transform, UILayerKind.Views, ViewsSortingOrder);
            _root.popups = CreateLayer<PopupLayer>(_go.transform, UILayerKind.Popups, PopupsSortingOrder);
            _root.popups.SetBackdrop(PopupLayer.CreateDefaultBackdrop(_root.popups.transform));
            _root.overlays = CreateLayer<OverlayLayer>(_go.transform, UILayerKind.Overlays, OverlaysSortingOrder);

            EnsureEventSystem();
            _go.SetActive(true);
            return _root;
        }

        private static T CreateLayer<T>(Transform _parent, UILayerKind _kind, int _sortingOrderBase) where T : ViewLayer {
            var _rect = new GameObject(_kind.ToString(), typeof(RectTransform)).GetComponent<RectTransform>();
            _rect.SetParent(_parent, false);
            _rect.anchorMin = Vector2.zero;
            _rect.anchorMax = Vector2.one;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;

            var _layer = _rect.gameObject.AddComponent<T>();
            _layer.Configure(_kind, _sortingOrderBase);
            return _layer;
        }

        private static void EnsureEventSystem() {
            if (EventSystem.current || FindAnyObjectByType<EventSystem>()) return;

            var _go = new GameObject("EventSystem", typeof(EventSystem));
            Type _inputSystemModule = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            _go.AddComponent(_inputSystemModule ?? typeof(StandaloneInputModule));
        }
        #endregion
    }
}
