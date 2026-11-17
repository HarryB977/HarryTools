using System;
using System.Collections.Generic;
using HarryTools.Conditions;
using UnityEngine;

namespace HarryTools.Dialogue {
    /// <summary> One case of a <see cref="BranchNode"/>: taken when its conditions pass </summary>
    [Serializable]
    public sealed class BranchCase {
        [SerializeField] private ConditionList conditions = new ConditionList();
        [SerializeField] private NodeOutput next = new NodeOutput();

        public ConditionList Conditions {
            get => conditions;
            internal set => conditions = value;
        }
        public NodeOutput Next => next;
    }

    /// <summary> Picks the first case whose conditions pass, or the otherwise output </summary>
    [Serializable]
    public sealed class BranchNode : DialogueNode {
        [SerializeField] private List<BranchCase> cases = new List<BranchCase>();
        [SerializeField] private NodeOutput otherwise = new NodeOutput();

        public List<BranchCase> Cases => cases;
        public NodeOutput Otherwise => otherwise;

        public override void CollectOutputs(List<NodeOutput> _into) {
            for (int _i = 0; _i < cases.Count; _i++) {
                if (cases[_i] != null) _into.Add(cases[_i].Next);
            }
            _into.Add(otherwise);
        }
    }
}
