using System;
using System.Collections.Generic;

using Cysharp.Threading.Tasks;
using HarryTools.LoggerTool;
using HarryUtils.Singleton;

namespace HarryTools.Views {
    public class ViewManager : Singleton<ViewManager> {
        // ==================== VARIABLES ===================
        #region Public Variables
        [ClearOnReload] public static Action<string> OnShowView = delegate { };
        [ClearOnReload] public static Action<string> OnHideView = delegate { };
        #endregion

        #region Private Variables
        private Dictionary<string, View> views;
        #endregion

        // ==================== START ====================
        private void Start() {
            views = new();
        }

        // ==================== METHODS ====================
        private void ViewNullError(string _view) {
            Logger.LogWarning("ViewManager", $"No se ha encontrado la view '{_view}'");
        }

        // =================================================

        private bool CheckView(string _viewName) {
            return views.ContainsKey(_viewName);
        }

        public bool CheckView<T>() where T : View {
            return views.ContainsKey(typeof(T).Name);
        }

        public View GetView<T>() where T : View {
            if (CheckView<T>()) return views[typeof(T).Name];
            else {
                ViewNullError(typeof(T).Name);
                return default;
            }
        }

        // =================================================

        public void RegisterView(View _view) {
            if (CheckView(_view.GetType().Name)) Logger.LogWarning("ViewManager", $"La view '{_view.GetType().Name}' ya se encuentra registrada");
            else views.Add(_view.GetType().Name, _view);
        }

        public void UnregisterView(View _view) {
            if (!CheckView(_view.GetType().Name)) Logger.LogWarning("ViewManager", $"La view '{_view.GetType().Name}' no se encuentra registrada");
            else views.Remove(_view.GetType().Name);
        }

        // =================================================

        #region Show
        public void ShowView<T>() where T : View {
            if (!CheckView<T>()) ViewNullError(typeof(T).Name);
            else {
                views[typeof(T).Name].Show();
                OnShowView?.Invoke(typeof(T).Name);
            }
        }

        public void ShowView<T>(View.ViewParameters _parameters) where T : View {
            UpdateParameters<T>(_parameters);
            ShowView<T>();
        }

        public void ShowView<T1, T2>() where T1 : View where T2 : View {
            UpdateDelegate<T1, T2>();
            ShowView<T1>();
        }

        public void ShowView<T1, T2>(View.ViewParameters _parameters) where T1 : View where T2 : View {
            UpdateParameters<T1>(_parameters);
            UpdateDelegate<T1, T2>();

            ShowView<T1>();
        }
        #endregion

        #region Hide
        public void HideView<T>() where T : View {
            if (!CheckView<T>()) ViewNullError(typeof(T).Name);
            else {
                views[typeof(T).Name].Hide();
                OnHideView?.Invoke(typeof(T).Name);
            }
        }
        #endregion

        // =================================================

        #region Wait Until
        public async UniTask WaitUntilShow<T>() where T : View {
            if (!CheckView<T>()) {
                ViewNullError(typeof(T).Name);
                return;
            }

            View _view = GetView<T>();
            while (!_view.IsShowing) await UniTask.Yield();
        }

        public async UniTask WaitUntilHide<T>() where T : View {
            if (!CheckView<T>()) {
                ViewNullError(typeof(T).Name);
                return;
            }

            View _view = GetView<T>();
            while (_view.IsShowing) await UniTask.Yield();
        }
        #endregion

        // =================================================

        public ViewManager UpdateParameters<T>(View.ViewParameters _parameters) where T : View {
            if (!CheckView<T>()) ViewNullError(typeof(T).Name);
            else views[typeof(T).Name].UpdateParameters(_parameters);

            return this;
        }

        public ViewManager UpdateDelegate<T1, T2>() where T1 : View where T2 : View {
            if (!CheckView<T1>()) {
                ViewNullError(typeof(T1).Name); return this;
            }

            if (!CheckView<T2>()) {
                ViewNullError(typeof(T2).Name); return this;
            }

            views[typeof(T1).Name].UpdateDelegate(views[typeof(T2).Name]);
            return this;
        }
    }
}