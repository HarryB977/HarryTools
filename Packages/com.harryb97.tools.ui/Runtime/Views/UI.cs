using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using HarryTools.DomainReload;

namespace HarryTools.UI {
    /// <summary> Acceso global al UIRoot activo: mostrar, ocultar y esperar vistas desde cualquier sitio </summary>
    public static class UI {
        // ==================== VARIABLES ===================
        #region Events
        /// <summary> Una vista de cualquier capa empieza a mostrarse </summary>
        [field: ClearOnReload] public static event Action<View> ViewShown;
        /// <summary> Una vista de cualquier capa termina de ocultarse </summary>
        [field: ClearOnReload] public static event Action<View> ViewHidden;
        #endregion

        #region Properties
        public static bool HasRoot => root;

        public static UIRoot Root => root
            ? root
            : throw new InvalidOperationException("There is no active UIRoot in the scene.");

        public static ViewLayer Views => Root.Views;
        public static PopupLayer Popups => Root.Popups;
        public static OverlayLayer Overlays => Root.Overlays;
        #endregion

        #region Private Variables
        [ClearOnReload] private static UIRoot root;
        #endregion

        // ==================== METHODS ====================
        /// <summary> Muestra la vista de tipo T en la capa de Views </summary>
        public static T Show<T>() where T : View => Views.Show<T>();

        /// <summary> Muestra una instancia o un prefab en la capa de Views </summary>
        public static T Show<T>(T _viewOrPrefab) where T : View => Views.Show(_viewOrPrefab);

        /// <summary> Muestra la vista de tipo T en la capa de Views y espera a que termine de entrar </summary>
        public static UniTask<T> ShowAsync<T>(CancellationToken _ct = default) where T : View => Views.ShowAsync<T>(_ct);

        /// <summary> Oculta la vista de tipo T buscando en Overlays, Popups y Views, en ese orden </summary>
        public static bool Hide<T>() where T : View => Overlays.Hide<T>() || Popups.Hide<T>() || Views.Hide<T>();

        /// <summary> Instancia de tipo T en cualquier capa, o null </summary>
        public static T Get<T>() where T : View => Overlays.Get<T>() ?? Popups.Get<T>() ?? Views.Get<T>();

        public static bool IsVisible<T>() where T : View => Overlays.IsVisible<T>() || Popups.IsVisible<T>() || Views.IsVisible<T>();

        /// <summary> Espera a que no quede ninguna vista de tipo T visible en ninguna capa </summary>
        public static UniTask WaitUntilHidden<T>(CancellationToken _ct = default) where T : View =>
            UniTask.WhenAll(Views.WaitUntilHidden<T>(_ct), Popups.WaitUntilHidden<T>(_ct), Overlays.WaitUntilHidden<T>(_ct));

        /// <summary> Instancia la vista de tipo T oculta en la capa de Views </summary>
        public static UniTask PreloadAsync<T>(CancellationToken _ct = default) where T : View => Views.PreloadAsync<T>(_ct);

        // =================================================

        internal static void Register(UIRoot _root) => root = _root;

        internal static void Unregister(UIRoot _root) {
            if (root == _root) root = null;
        }

        internal static void RaiseShown(View _view) => ViewShown?.Invoke(_view);
        internal static void RaiseHidden(View _view) => ViewHidden?.Invoke(_view);
    }
}
