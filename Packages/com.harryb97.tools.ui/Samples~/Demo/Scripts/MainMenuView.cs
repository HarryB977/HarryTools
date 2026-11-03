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
            UI.Popups.Show<ConfirmPopup>().Setup("Hola", "Esto es un popup. Haz clic fuera o en Aceptar para cerrarlo.");
        }

        private void ShowTwoPopups() {
            UI.Popups.Show<ConfirmPopup>().Setup("Primero", "El segundo popup está esperando en la cola.");
            UI.Popups.Show<ConfirmPopup>().Setup("Segundo", "Este popup estaba en la cola y ha aparecido al cerrar el primero.");
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
