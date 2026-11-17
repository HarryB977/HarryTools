using System;
using System.Collections.Generic;
using UnityEngine;

namespace HarryTools.Dialogue {
    /// <summary> Entry point of a dialogue. Every asset has exactly one </summary>
    [Serializable]
    public sealed class StartNode : DialogueNode {
        [SerializeField] private NodeOutput next = new NodeOutput();

        public NodeOutput Next => next;

        public override void CollectOutputs(List<NodeOutput> _into) {
            _into.Add(next);
        }
    }
}
