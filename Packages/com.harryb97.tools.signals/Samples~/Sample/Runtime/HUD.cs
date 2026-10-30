using UnityEngine;
using UnityEngine.UI;

namespace HarryTools.Signals.Sample {
    public class HUD : MonoBehaviour {
        // ==================== VARIABLES ===================
        #region Signals
        private IReadonlySignal<int> playerHealthSignal;
        #endregion

        #region Private Variables
        [SerializeField] private Text playerHealthLabel;
        #endregion

        // ==================== START ====================
        private async void Start() {
            // Signals
            playerHealthSignal = await Signal<int>.GetReadonlyAsync("PlayerHealth");
            playerHealthSignal.AddListener(Event_OnPlayerHealthChange);

            // Labels
            UpdatePlayerHealthLabel(playerHealthSignal.Value);
        }

        // ==================== EVENTS ====================
        private void Event_OnPlayerHealthChange(int _health) {
            UpdatePlayerHealthLabel(_health);
        }

        // ==================== METHODS ====================
        private void UpdatePlayerHealthLabel(int _health) {
            playerHealthLabel.text = $"Vida: {_health}";
        }
    }
}