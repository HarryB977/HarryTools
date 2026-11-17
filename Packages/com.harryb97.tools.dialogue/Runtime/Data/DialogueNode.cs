using System;
using System.Collections.Generic;
using UnityEngine;

namespace HarryTools.Dialogue {
    /// <summary> Base of every node stored in a <see cref="DialogueAsset"/> </summary>
    [Serializable]
    public abstract class DialogueNode {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private string id = "";
        [SerializeField] private Vector2 position;
        #endregion

        #region Public Variables
        public string Id {
            get => id;
            internal set => id = value;
        }

        /// <summary> Position of the node in the graph editor </summary>
        public Vector2 Position {
            get => position;
            internal set => position = value;
        }
        #endregion

        // ==================== METHODS ====================
        /// <summary> Adds every output of this node, in display order </summary>
        public abstract void CollectOutputs(List<NodeOutput> _into);
    }
}
