using System;
using UnityEngine;

namespace HarryTools.Dialogue {
    /// <summary> One output port of a node, option or branch case. Points to the id of the node it leads to; empty means the dialogue ends </summary>
    [Serializable]
    public sealed class NodeOutput {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private string id = "";
        [SerializeField] private string target = "";
        #endregion

        #region Public Variables
        public string Id {
            get => id;
            internal set => id = value;
        }

        public string Target {
            get => target;
            internal set => target = value;
        }

        public bool IsConnected => !string.IsNullOrEmpty(target);
        #endregion
    }
}
