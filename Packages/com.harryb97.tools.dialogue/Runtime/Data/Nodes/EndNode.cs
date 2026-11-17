using System;
using System.Collections.Generic;

namespace HarryTools.Dialogue {
    /// <summary> Ends the dialogue </summary>
    [Serializable]
    public sealed class EndNode : DialogueNode {
        public override void CollectOutputs(List<NodeOutput> _into) { }
    }
}
