using UnityEngine;

namespace HarryTools.Signals.Sample {
    public class Enemy : MonoBehaviour {
        // ==================== VARIABLES ===================
        #region Signals
        private Signal<int> playerHealthSignal;
        #endregion

        // ==================== START ====================
        private async void Start() {
            // Signals
            playerHealthSignal = await Signal<int>.GetAsync("PlayerHealth");
        }
        
        private void Update() {
            if (Input.GetKeyDown(KeyCode.Space)) {
                int _damage = Random.Range(5, 15);
                playerHealthSignal.Value -= _damage;
            }
        }
    }
}