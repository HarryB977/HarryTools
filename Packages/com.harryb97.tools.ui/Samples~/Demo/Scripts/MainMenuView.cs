using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace HarryTools.UI.Demo {
    /// <summary> Menú de la demo: abre un popup, dos popups en cola y una carga con ShowWhile </summary>
    public class MainMenuView : View {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private Button popupButton;
        [SerializeField] private Button twoPopupsButton;
        [SerializeField] private Button loadButton;
        #endregion

        // ==================== START ====================
        protected override void Awake() {
            base.Awake();

            popupButton.onClick.AddListener(ShowPopup);
            twoPopupsButton.onClick.AddListener(ShowTwoPopups);
            loadButton.onClick.AddListener(Load);
        }

        // ==================== METHODS ====================
        private void ShowPopup() {
            UI.Popups.Show<ConfirmPopup>().Setup("Hello", "This is a popup. Click outside or on OK to close it.");
        }

        private void ShowTwoPopups() {
            UI.Popups.Show<ConfirmPopup>().Setup("First", "The second popup is waiting in the queue.");
            UI.Popups.Show<ConfirmPopup>().Setup("Second", "This popup was queued and appeared when the first one closed.");
        }

        private void Load() {
            UI.Overlays.ShowWhile<DemoLoadingOverlay>(SimulatedLoad, destroyCancellationToken).Forget();
        }

        /// <summary> Simula una carga de 2 segundos informando del progreso </summary>
        private static async UniTask SimulatedLoad(IProgress<float> _progress, CancellationToken _ct) {
            const int steps = 20;

            for (int i = 0; i <= steps; i++) {
                _progress.Report(i / (float)steps);
                await UniTask.Delay(100, DelayType.UnscaledDeltaTime, cancellationToken: _ct);
            }
        }
    }
}
