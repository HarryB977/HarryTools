using UnityEngine;

namespace HarryTools.StateMachineTool.Example {
    [RequireComponent(typeof(Rigidbody))]
    public class Player : Entity {
        // ==================== VARIABLES ===================
        #region Properties
        private bool IsMoving => Input.GetAxis("Horizontal") != 0 || Input.GetAxis("Vertical") != 0;

        private float HorizontalInput => Input.GetAxis("Horizontal") * (speed * Time.deltaTime);
        private float VerticalInput => Input.GetAxis("Vertical") * (speed * Time.deltaTime);
        #endregion
        
        #region Private Variables
        [SerializeField] private float speed = 2f;
        
        private StateMachine stateMachine;

        // References
        private Rigidbody rb;
        #endregion
        
        // ==================== START ====================
        private void Awake() {
            // References
            TryGetComponent(out rb);
        }
        
        private void Start() {
            InitializeStateMachine();
        }

        private void Update() {
            stateMachine.Ticks();
        }

        private void FixedUpdate() {
            stateMachine.FixedTicks();
        }
        
        // ==================== EVENTS ====================
        private void Event_OnChangeState(IState _state) {
            UpdatePlayerName($"Player - {_state.GetType().Name}");
        }
        
        // ==================== METHODS ====================
        private void InitializeStateMachine() {
            // Create States
            IdleState _idleState = new(this);
            MovementState _movementState = new(this);

            // Create State Machine
            stateMachine = new StateMachine()
                .AddTransition(_idleState, _movementState, new FuncPredicate(() => IsMoving))
                .AddTransition(_movementState, _idleState, new FuncPredicate(() => !IsMoving))
                .SetState(_idleState)
            ;

            // Events
            stateMachine.OnChangeState += Event_OnChangeState;

            // Settings
            UpdatePlayerName($"Player - {_idleState.GetType().Name}");
        }

        private void UpdatePlayerName(string _name) {
            gameObject.name = _name;
        }
        
        // =================================================

        public void Movement() {
            rb.linearVelocity += new Vector3(HorizontalInput, 0, VerticalInput);
        }
    }
}