using UnityEngine;

namespace HarryTools.StateMachineTool.Example {
    public class PlayerState : BaseState {
        // ==================== VARIABLES ===================
        #region Private Variables
        protected readonly Player player;
        #endregion
        
        // ==================== START ====================
        protected PlayerState(Player _player) : base() {
            player = _player;
        }
    }

    public class IdleState : PlayerState {
        // ==================== START ====================
        public IdleState(Player _player) : base(_player) { }

        // ==================== METHODS ====================
        public override void OnEnter() {
            Debug.Log("Entered Idle State!");
        }
    }

    public class MovementState : PlayerState {
        // ==================== START ====================
        public MovementState(Player _player) : base(_player) { }

        // ==================== METHODS ====================
        public override void OnEnter() {
            Debug.Log("Entered Movement State!");
        }

        public override void Ticks() {
            player.Movement();
        }
    }
}