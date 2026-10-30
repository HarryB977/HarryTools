using System.Collections.Generic;

using HarryTools.UICanvas;

using Cysharp.Threading.Tasks;
using HarryTools.LoggerTool;
using HarryUtils;
using HarryUtils.Singleton;

namespace HarryTools.Popups {
    public class PopupManager : Singleton<PopupManager> {
        // ==================== VARIABLES ===================
        #region Public Variables
        public bool RemainingPopups => !popupsQueue.IsNullOrEmpty();
        public bool ShowingPopups { get; private set; }
        #endregion
        
        #region Private Variables
        private Dictionary<string, Popup> popups;
        private Queue<Popup> popupsQueue;
        #endregion
        
        // ==================== START ====================
        private void Start() {
            popups = new();
            popupsQueue = new();
        }
        
        // ==================== METHODS ====================
        private void ViewNullError(string _popup) {
            Logger.LogWarning("PopupManager", $"No se ha encontrado el popup '{_popup}'");
        }
        
        // =================================================

        private bool CheckPopup(string _popupName) {
            return popups.ContainsKey(_popupName);
        }

        public void RegisterPopup(Popup _popup) {
            if (CheckPopup(_popup.GetType().Name)) Logger.LogWarning("PopupManager", $"El popup '{_popup.GetType().Name}' ya se encuentra registrado");
            else {
                _popup.OnHide += OnPopupHide;
                popups.Add(_popup.GetType().Name, _popup);
            }
        }

        public void UnregisterPopup(Popup _popup) {
            if (!CheckPopup(_popup.GetType().Name)) Logger.LogWarning("PopupManager", $"El popup '{_popup.GetType().Name}' no se encuentra registrado");
            else {
                _popup.OnHide -= OnPopupHide;
                popups.Remove(_popup.GetType().Name);
            }
        }

        // =================================================

        public bool CheckPopup<T>() where T : Popup {
            return popups.ContainsKey(typeof(T).Name);
        }

        public Popup GetPopup<T>() where T : Popup {
            if (CheckPopup<T>()) return popups[typeof(T).Name];
            else {
                ViewNullError(typeof(T).Name);
                return default;
            }
        }

        // =================================================

        #region Show
        public void ShowPopup<T>() where T : Popup {
            if (!CheckPopup<T>()) {
                ViewNullError(typeof(T).Name);
                return;
            }

            Popup _popup = GetPopup<T>();

            if (!_popup.DoesRespectQueue) {
                _popup.Show();
                return;
            }

            if (ShowingPopups) {
                popupsQueue.Enqueue(_popup);
                return;
            }

            ShowingPopups = true;
            _popup.Show();
        }

        public void ShowPopup<T>(Popup.PopupParameters _parameters) where T : Popup {
            UpdateParameters<T>(_parameters);
            ShowPopup<T>();
        }

        public void ShowPopup<T1, T2>() where T1 : Popup where T2 : Popup {
            UpdateDelegate<T1, T2>();
            ShowPopup<T1>();
        }

        public void ShowPopup<T1, T2>(Popup.PopupParameters _parameters) where T1 : Popup where T2 : Popup {
            UpdateParameters<T1>(_parameters);
            UpdateDelegate<T1, T2>();
            
            ShowPopup<T1>();
        }
        #endregion

        #region Hide
        public void HidePopup<T>() where T : Popup {
            if (!CheckPopup<T>()) ViewNullError(typeof(T).Name);
            else popups[typeof(T).Name].Hide();
        }

        public void HideCurrentPopup() {
            if (popups.IsNullOrEmpty()) return;
            
            foreach (KeyValuePair<string, Popup> _popup in popups) {
                if (!_popup.Value.IsShowing) continue;
                
                _popup.Value.Hide();
                break;
            }
        }

        public void HideAllPopups() {
            if (popups.IsNullOrEmpty()) return;

            popupsQueue = new();
            HideCurrentPopup();
        }
        #endregion
        
        // =================================================

        #region Wait Until
        public async UniTask WaitUntilShow<T>() where T : Popup {
            if (!CheckPopup<T>()) {
                ViewNullError(typeof(T).Name);
                return;
            }

            Popup _popup = GetPopup<T>();
            while (!_popup.IsShowing) await UniTask.Yield();
        }

        public async UniTask WaitUntilHide<T>() where T : Popup {
            if (!CheckPopup<T>()) {
                ViewNullError(typeof(T).Name);
                return;
            }

            Popup _popup = GetPopup<T>();
            while (_popup.IsShowing) await UniTask.Yield();
        }
        #endregion
        
        // =================================================

        public PopupManager UpdateParameters<T>(Popup.PopupParameters _parameters) where T : Popup {
            if (!CheckPopup<T>()) ViewNullError(typeof(T).Name);
            else popups[typeof(T).Name].UpdateParameters(_parameters);

            return this;
        }

        public PopupManager UpdateDelegate<T1, T2>() where T1 : Popup where T2 : Popup {
            if (!CheckPopup<T1>()) {
                ViewNullError(typeof(T1).Name); return this;
            }

            if (!CheckPopup<T2>()) {
                ViewNullError(typeof(T2).Name); return this;
            }

            popups[typeof(T1).Name].UpdateDelegate(popups[typeof(T2).Name]);
            return this;
        }

        // =================================================

        #region Callbacks
        private void OnPopupHide(CustomCanvas<Popup, Popup.PopupParameters> _popup) {
            if (RemainingPopups) popupsQueue.Dequeue().Show();
            else ShowingPopups = false;
        }
        #endregion
    }
}