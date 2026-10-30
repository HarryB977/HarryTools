using UnityEngine;

namespace HarryTools.Platformer2DController.Sample {
    public class SinPlatform : Platform {
        // ==================== VARIABLES ===================
        #region Public Variables
        #endregion

        #region Private Variables
        [Header("Sin Platform")]
        [SerializeField] private float movementSpeed = 1f;
        [SerializeField] private float movementDistance = 1f;
        #endregion

        // ==================== METHODS ====================
        protected override Vector2 Evaluate(float _deltaTime) {
            return new Vector2(Mathf.Sin(Time.time * movementSpeed) * movementDistance, rb.position.y);
        }
    }
}