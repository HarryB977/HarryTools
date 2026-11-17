using System;
using System.Collections.Generic;
using HarryTools.Conditions;
using UnityEngine;

namespace HarryTools.Dialogue {
    /// <summary> A choice offered by a <see cref="DialogueRun"/>. Indices refer to <see cref="Options"/>; unavailable options cannot be selected or chosen </summary>
    public sealed class ChoicePrompt {
        // ==================== VARIABLES ===================
        #region Private Variables
        private readonly List<ChoiceOptionState> options;
        private readonly float startClock;
        private readonly float duration;
        #endregion

        #region Public Variables
        public DialogueRun Run { get; }
        public ChoiceNode Node { get; }

        /// <summary> The visible options in order. Hidden ones are not listed </summary>
        public IReadOnlyList<ChoiceOptionState> Options => options;

        /// <summary> Index of the selected option, or -1 when none is </summary>
        public int SelectedIndex { get; private set; } = -1;

        public bool HasTimer { get; }

        /// <summary> Time left on the timer, from 1 to 0. Always 1 without timer </summary>
        public float Remaining01 => HasTimer ? Mathf.Clamp01(1f - (Run.Clock - startClock) / duration) : 1f;

        /// <summary> Raised when the selection changes </summary>
        public event Action Changed;
        #endregion

        #region Internal Variables
        /// <summary> False once an option or the no answer output was taken, or the run ended </summary>
        internal bool IsOpen { get; set; } = true;

        internal bool TimedOut => HasTimer && Run.Clock - startClock >= duration;

        internal bool HasAvailable {
            get {
                for (int _i = 0; _i < options.Count; _i++) {
                    if (options[_i].Available) return true;
                }
                return false;
            }
        }
        #endregion

        // ==================== START ====================
        /// <summary> Evaluates every option of the node against its conditions and the dialogue memory </summary>
        internal ChoicePrompt(DialogueRun _run, ChoiceNode _node, LogicContext _context) {
            Run = _run;
            Node = _node;
            HasTimer = _node.UseTimer && _node.TimerSeconds > 0f;
            duration = _node.TimerSeconds;
            startClock = _run.Clock;

            options = new List<ChoiceOptionState>(_node.Options.Count);
            for (int _i = 0; _i < _node.Options.Count; _i++) {
                ChoiceOption _option = _node.Options[_i];
                if (_option == null) continue;
                bool _chosen = DialogueMemory.WasChosen(_run.CurrentAsset, _option.Id);
                if (_chosen && _option.AfterChosen == AfterChosen.Hide) continue;
                if (_option.Visible != null && !_option.Visible.Check(_context)) continue;

                bool _available = (_option.Available == null || _option.Available.Check(_context))
                    && !(_chosen && _option.AfterChosen == AfterChosen.Disable);
                bool _seen = _chosen && _option.AfterChosen == AfterChosen.MarkSeen;
                options.Add(new ChoiceOptionState(_option, _available, _seen));
            }

            if (DialogueSettings.Current.InitialSelection == InitialSelection.DefaultOrFirst) SelectedIndex = FallbackIndex();
        }

        // ==================== CONTROL ====================
        #region Control
        /// <summary> Moves the selection by the given steps over the available options, wrapping around. From no selection, forward starts at the first and backward at the last </summary>
        public void MoveSelection(int _delta) {
            if (!IsOpen || _delta == 0 || !HasAvailable) return;
            int _count = options.Count;
            int _direction = Math.Sign(_delta);
            int _index = SelectedIndex;
            if (_index < 0) _index = _direction > 0 ? -1 : _count;

            for (int _step = 0; _step < Math.Abs(_delta); _step++) {
                do {
                    _index = ((_index + _direction) % _count + _count) % _count;
                } while (!options[_index].Available);
            }
            SetSelected(_index);
        }

        /// <summary> Selects the option, or clears the selection with -1. Unavailable options are ignored </summary>
        public void Select(int _index) {
            if (!IsOpen) return;
            if (_index == -1) {
                SetSelected(-1);
                return;
            }
            if (!IsChoosable(_index)) return;
            SetSelected(_index);
        }

        /// <summary> Chooses the selected option, if any </summary>
        public void Confirm() {
            Choose(SelectedIndex);
        }

        /// <summary> Chooses the option. Unavailable options are ignored </summary>
        public void Choose(int _index) {
            if (!IsOpen || !IsChoosable(_index)) return;
            Run.RequestChoice(this, options[_index].Option);
        }
        #endregion

        // ==================== INTERNAL ====================
        #region Internal
        /// <summary> The selected option, then the first default one, then the first one. Only available options count; null when none is </summary>
        internal ChoiceOption Fallback() {
            if (IsChoosable(SelectedIndex)) return options[SelectedIndex].Option;
            int _index = FallbackIndex();
            return _index >= 0 ? options[_index].Option : null;
        }
        #endregion

        // ==================== PRIVATE ====================
        #region Private Methods
        /// <summary> The first available default option, then the first available one. -1 when none is available </summary>
        private int FallbackIndex() {
            for (int _i = 0; _i < options.Count; _i++) {
                if (options[_i].Available && options[_i].IsDefault) return _i;
            }
            for (int _i = 0; _i < options.Count; _i++) {
                if (options[_i].Available) return _i;
            }
            return -1;
        }

        private bool IsChoosable(int _index) {
            return _index >= 0 && _index < options.Count && options[_index].Available;
        }

        private void SetSelected(int _index) {
            if (_index == SelectedIndex) return;
            SelectedIndex = _index;
            Changed?.Invoke();
        }
        #endregion
    }
}
