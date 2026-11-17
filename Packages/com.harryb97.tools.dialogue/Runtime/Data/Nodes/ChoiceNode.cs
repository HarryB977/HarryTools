using System;
using System.Collections.Generic;
using UnityEngine;

namespace HarryTools.Dialogue {
    /// <summary> Offers the player a set of options </summary>
    [Serializable]
    public sealed class ChoiceNode : DialogueNode {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private List<ChoiceOption> options = new List<ChoiceOption>();
        [SerializeField] private bool useTimer;
        [SerializeField, Min(0.1f)] private float timerSeconds = 10f;
        [SerializeField] private TimeoutRule timeoutRule = TimeoutRule.ChooseSelected;
        [Tooltip("Taken on timeout with the no answer rule, or when no option can be offered"), SerializeField] private NodeOutput noAnswer = new NodeOutput();
        #endregion

        #region Public Variables
        public List<ChoiceOption> Options => options;

        public bool UseTimer {
            get => useTimer;
            internal set => useTimer = value;
        }

        public float TimerSeconds {
            get => timerSeconds;
            internal set => timerSeconds = value;
        }

        public TimeoutRule TimeoutRule {
            get => timeoutRule;
            internal set => timeoutRule = value;
        }

        public NodeOutput NoAnswer => noAnswer;
        #endregion

        // ==================== METHODS ====================
        /// <summary> Options in order, then the no answer output, which always exists as the way out when every option is blocked </summary>
        public override void CollectOutputs(List<NodeOutput> _into) {
            for (int _i = 0; _i < options.Count; _i++) {
                if (options[_i] != null) _into.Add(options[_i].Next);
            }
            _into.Add(noAnswer);
        }
    }
}
