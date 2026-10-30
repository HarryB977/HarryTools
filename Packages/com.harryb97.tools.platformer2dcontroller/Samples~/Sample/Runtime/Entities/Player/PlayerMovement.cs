using UnityEngine;

namespace HarryTools.Platformer2DController.Sample {
    [RequireComponent(typeof(Player))]
    public class PlayerMovement : MovementController {
        // ==================== VARIABLES ===================
        #region Properties
        // Settings
        protected override ControllerData Data => player.GetData;

        // Inputs
        protected override Vector2 MoveInput => new(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        protected override bool JumpDownInput => Input.GetKeyDown(KeyCode.Space);
        protected override bool JumpHeldInput => Input.GetKey(KeyCode.Space);
        protected override bool DashDownInput => Input.GetKeyDown(KeyCode.X);
        #endregion

        #region Private Variables
        // References
        private Player player;
        #endregion
        
        // ==================== START ====================
        private void Awake() {
            // References
            TryGetComponent(out player);
        }

        private void Start() {
            // Controller
            InitializeController();
        }

        private void Update() {
            MovementTicks();
        }

        private void FixedUpdate() {
            MovementFixedTicks();
        }
    }
}