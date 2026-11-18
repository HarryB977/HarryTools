using System;
using System.Collections.Generic;
using UnityEngine;

namespace HarryTools.Dialogue {
    /// <summary> A titled, colored box around a set of nodes in the graph window. It has no effect when the dialogue runs </summary>
    [Serializable]
    public sealed class DialogueGroup {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private string id = "";
        [SerializeField] private string title = "";
        [SerializeField] private Color color = new Color(0.25f, 0.45f, 0.75f, 1f);
        [SerializeField] private List<string> nodeIds = new List<string>();
        #endregion

        #region Public Variables
        public string Id {
            get => id;
            internal set => id = value;
        }

        public string Title {
            get => title;
            internal set => title = value;
        }

        public Color Color {
            get => color;
            internal set => color = value;
        }

        /// <summary> Ids of the nodes inside the group </summary>
        public IReadOnlyList<string> NodeIds => nodeIds;
        #endregion

        #region Internal Variables
        internal List<string> NodeIdList => nodeIds;
        #endregion
    }
}
