using UnityEngine;

namespace HarryTools.Signals.Sample {
    public class Player : MonoBehaviour {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private int health = 100;
        #endregion

        // ==================== START ====================
        private void Awake() {
            Signal<int>.New()
                .WithId("PlayerHealth")
                .WithValue(health)
                .Build();
        }
    }
}