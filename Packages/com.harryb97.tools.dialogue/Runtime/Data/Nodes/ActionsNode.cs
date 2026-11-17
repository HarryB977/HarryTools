using System;
using System.Collections.Generic;
using HarryTools.Actions;
using UnityEngine;

namespace HarryTools.Dialogue {
    /// <summary> Runs a list of actions between lines </summary>
    [Serializable]
    public sealed class ActionsNode : DialogueNode {
        [SerializeField] private ActionList actions = new ActionList();
        [Tooltip("The dialogue waits for the actions to finish before continuing"), SerializeField] private bool waitForCompletion = true;
        [Tooltip("The actions still run when the dialogue is skipped"), SerializeField] private bool runWhenSkipped = true;
        [SerializeField] private NodeOutput next = new NodeOutput();

        public ActionList Actions => actions;

        public bool WaitForCompletion {
            get => waitForCompletion;
            internal set => waitForCompletion = value;
        }

        public bool RunWhenSkipped {
            get => runWhenSkipped;
            internal set => runWhenSkipped = value;
        }

        public NodeOutput Next => next;

        public override void CollectOutputs(List<NodeOutput> _into) {
            _into.Add(next);
        }
    }
}
