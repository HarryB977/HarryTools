using UnityEngine;

namespace HarryTools.Platformer3DController.Sample {
    /// <summary> Top down blob shadow under the player that shrinks with the height, it makes jumps easier to land. </summary>
    public class BlobShadow : MonoBehaviour {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private Player player;
        [SerializeField] private Renderer shadowRenderer;

        [Header("Shape")]
        [SerializeField] private float size = 1f;
        [SerializeField] private float groundOffset = 0.02f;

        [Header("Ground Check")]
        [SerializeField] private float checkRadius = 0.25f;
        [SerializeField] private float checkDistance = 12f;
        [SerializeField] private LayerMask checkMask = ~0;

        private readonly GroundCheck ground = new();
        #endregion

        // ==================== START ====================
        private void LateUpdate() {
            if (player == null || player.Movement == null) return;

            Rigidbody _body = player.GetComponent<Rigidbody>();
            Vector3 _up = player.Movement.Up;
            bool _hit = ground.Check(_body.position, -_up, checkRadius, checkDistance, checkMask, _body);

            shadowRenderer.enabled = _hit;
            if (!_hit) return;

            // The ground below the player is used instead of the hit point, so the shadow does not slide on edges
            float _ratio = 1f - ground.Distance / checkDistance;
            transform.SetPositionAndRotation(ground.Point + ground.Hit.normal * groundOffset, Quaternion.FromToRotation(Vector3.up, ground.Hit.normal));
            transform.localScale = Vector3.one * (size * _ratio);
        }
    }
}
