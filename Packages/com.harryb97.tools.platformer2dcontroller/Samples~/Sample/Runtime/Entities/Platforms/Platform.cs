using UnityEngine;

namespace HarryTools.Platformer2DController.Sample {
    [RequireComponent(typeof(Rigidbody2D))]
    public abstract class Platform : MonoBehaviour, IPhysicsObjectMove {
        // ==================== VARIABLES ===================
        #region Private Variables
        private Vector2 FramePositionDelta { get; set; }
        
        [Header("Platform Settings")]
        [SerializeField] private bool usesBounding = true;
        [SerializeField] private bool requireGrounding = false;
        [SerializeField] private bool useTakeOffVelocity = false;

        // References
        protected Rigidbody2D rb;
        #endregion

        // ==================== START ====================
        protected virtual void Awake() {
            // References
            TryGetComponent(out rb);
        }

        protected virtual void Start() {
            // Settings
            rb.bodyType = RigidbodyType2D.Kinematic;
        }

        protected virtual void FixedUpdate() {
            PlatformMovement();
        }

        // ==================== METHODS ====================
        private void PlatformMovement() {
            Vector2 _newPosition = Evaluate(Time.deltaTime);
            Vector2 _positionDifference = _newPosition - rb.position;

            FramePositionDelta = _positionDifference.sqrMagnitude > 0 ? _positionDifference : Vector2.zero;
            rb.linearVelocity = _positionDifference.sqrMagnitude > 0 ? FramePositionDelta / Time.deltaTime : Vector2.zero;
        }

        protected abstract Vector2 Evaluate(float _deltaTime);
        
        // ==================== INTERFACES ====================
        #region IPhysicsObjectMove
        bool IPhysicsObjectMove.UsesBounding => usesBounding;
        bool IPhysicsObjectMove.RequireGrounding => requireGrounding;
        
        Vector2 IPhysicsObjectMove.FramePositionDelta => FramePositionDelta;
        Vector2 IPhysicsObjectMove.FramePosition => rb.position;
        Vector2 IPhysicsObjectMove.Velocity => rb.linearVelocity;
        Vector2 IPhysicsObjectMove.TakeOffVelocity => useTakeOffVelocity ? rb.linearVelocity : Vector2.zero;
        #endregion
    }
}