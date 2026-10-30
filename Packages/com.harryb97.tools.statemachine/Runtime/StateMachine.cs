using System;
using System.Collections.Generic;

namespace HarryTools.StateMachineTool {
    public class StateMachine {
        private class StateNode {
            public IState State { get; }
            public HashSet<ITransition> Transitions { get; }

            public StateNode(IState _state) {
                State = _state;
                Transitions = new();
            }

            public void AddTransition(IState _to, IPredicate _condition) {
                Transitions.Add(new Transition(_to, _condition));
            }
        }

        // ==================== VARIABLES ===================
        #region Public Variables
        public event Action<IState> OnSetState;
        public event Action<IState> OnChangeState;
        #endregion

        #region Private Variables
        private StateNode current;
        private Dictionary<Type, StateNode> nodes = new();
        private HashSet<ITransition> anyTransitions = new();
        #endregion

        // ==================== START ====================
        public StateMachine() {
            nodes = new();
            anyTransitions = new();
        }

        public void Ticks() {
            ITransition _transition = GetTransition();

            if (_transition != null) {
                ChangeState(_transition.To);
            }

            current.State?.Ticks();
        }

        public void FixedTicks() {
            current.State?.FixedTicks();
        }


        // ==================== METHODS ====================
        public StateMachine SetState(IState _state) {
            current = nodes[_state.GetType()];
            current.State?.OnEnter();

            OnSetState?.Invoke(_state);

            return this;
        }

        private StateMachine ChangeState(IState _state) {
            if (_state == current.State) return this;

            IState _previousState = current.State;
            IState _nextState = nodes[_state.GetType()].State;
            
            _previousState?.OnExit();
            _nextState?.OnEnter();

            current = nodes[_state.GetType()];

            OnChangeState?.Invoke(_state);
            return this;
        }

        // =================================================

        public StateMachine AddTransition(IState _from, IState _to, IPredicate _condition) {
            GetOrAddNode(_from).AddTransition(GetOrAddNode(_to).State, _condition);
            return this;
        }

        public StateMachine AddAnyTransition(IState _to, IPredicate _condition) {
            anyTransitions.Add(new Transition(GetOrAddNode(_to).State, _condition));
            return this;
        }

        private ITransition GetTransition() {
            foreach (ITransition _transition in anyTransitions) {
                if (_transition.Condition.Evaluate()) {
                    return _transition;
                }
            }

            foreach (ITransition _transition in current.Transitions) {
                if (_transition.Condition.Evaluate()) {
                    return _transition;
                }
            }

            return null;
        }

        // =================================================

        private StateNode GetOrAddNode(IState _state) {
            StateNode _node = nodes.GetValueOrDefault(_state.GetType());

            if (_node == null) {
                _node = new StateNode(_state);
                nodes.Add(_state.GetType(), _node);
            }

            return _node;
        }
    }
}