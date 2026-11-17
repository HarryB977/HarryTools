using System;
using System.Collections.Generic;
using UnityEngine;

namespace HarryTools.Dialogue {
    public enum JumpMode {
        [Tooltip("Leaves this dialogue and continues in the target")] Continue,
        [Tooltip("Runs the target and comes back to this node's output when it ends")] CallAndReturn
    }

    /// <summary> Moves the conversation into another dialogue asset </summary>
    [Serializable]
    public sealed class JumpNode : DialogueNode {
        [SerializeField] private DialogueAsset target;
        [SerializeField] private JumpMode mode = JumpMode.Continue;
        [SerializeField] private NodeOutput next = new NodeOutput();

        public DialogueAsset Target {
            get => target;
            internal set => target = value;
        }

        public JumpMode Mode {
            get => mode;
            internal set => mode = value;
        }

        public NodeOutput Next => next;

        public override void CollectOutputs(List<NodeOutput> _into) {
            _into.Add(next);
        }
    }
}
