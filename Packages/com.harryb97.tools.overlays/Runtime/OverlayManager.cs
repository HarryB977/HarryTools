using System.Collections.Generic;

using Cysharp.Threading.Tasks;
using HarryTools.LoggerTool;
using HarryUtils.Singleton;

namespace HarryTools.Overlays {
    public class OverlayManager : Singleton<OverlayManager> {
        // ==================== VARIABLES ===================
        #region Private Variables
        private Dictionary<string, Overlay> overlays;
        private Overlay currentOverlay;
        #endregion
        
        // ==================== START ====================
        private void Start() {
            overlays = new();
        }
            
        // ==================== METHODS ====================
        private void OverlayNullError(string _overlay) {
            Logger.LogWarning("ViewManager", $"No se ha encontrado el overlay '{_overlay}'");
        }

        // =================================================
        
        private bool CheckOverlay(string _overlayName) {
            return overlays.ContainsKey(_overlayName);
        }

        public bool CheckOverlay<T>() where T : Overlay {
            return overlays.ContainsKey(typeof(T).Name);
        }

        public Overlay GetOverlay<T>() where T : Overlay {
            if (CheckOverlay<T>()) return overlays[typeof(T).Name];
            else {
                OverlayNullError(typeof(T).Name);
                return default;
            }
        }
        
        // =================================================

        public void RegisterOverlay(Overlay _overlay) {
            if (CheckOverlay(_overlay.GetType().Name)) Logger.LogWarning("ViewManager", $"La view '{_overlay.GetType().Name}' ya se encuentra registrada");
            else overlays.Add(_overlay.GetType().Name, _overlay);
        }

        public void UnregisterOverlay(Overlay _overlay) {
            if (!CheckOverlay(_overlay.GetType().Name)) Logger.LogWarning("ViewManager", $"La view '{_overlay.GetType().Name}' no se encuentra registrada");
            else overlays.Remove(_overlay.GetType().Name);
        }
        
        // =================================================

        #region Show
        public void ShowOverlay<T>() where T : Overlay {
            if (!CheckOverlay<T>()) OverlayNullError(typeof(T).Name);
            else {
                HideOverlay();
                
                currentOverlay = overlays[typeof(T).Name];
                currentOverlay.Show();
            }
        }

        public void ShowOverlay<T>(Overlay.OverlayParameters _parameters) where T : Overlay {
            UpdateParameters<T>(_parameters);
            ShowOverlay<T>();
        }
        #endregion

        #region Hide
        public void HideOverlay() {
            currentOverlay?.Hide();
        }
        #endregion
        
        // =================================================

        #region Wait Until
        public async UniTask WaitUntilShow<T>() where T : Overlay {
            if (!CheckOverlay<T>()) {
                OverlayNullError(typeof(T).Name);
                return;
            }

            Overlay _overlay = GetOverlay<T>();
            while (!_overlay.IsShowing) await UniTask.Yield();
        }

        public async UniTask WaitUntilHide() {
            if (currentOverlay == null) return;
            while (currentOverlay.IsShowing) await UniTask.Yield();
        }
        #endregion

        // =================================================
        
        public OverlayManager UpdateParameters<T>(Overlay.OverlayParameters _parameters) where T : Overlay {
            if (!CheckOverlay<T>()) OverlayNullError(typeof(T).Name);
            else overlays[typeof(T).Name].UpdateParameters(_parameters);

            return this;
        }
    }
}