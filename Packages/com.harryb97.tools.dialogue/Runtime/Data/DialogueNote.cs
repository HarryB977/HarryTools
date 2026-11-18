using System;
using UnityEngine;

namespace HarryTools.Dialogue {
    /// <summary> A free text note placed in the graph window. It has no effect when the dialogue runs </summary>
    [Serializable]
    public sealed class DialogueNote {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private string id = "";
        [SerializeField] private string text = "";
        [SerializeField] private Rect rect = new Rect(0f, 0f, 200f, 100f);
        #endregion

        #region Public Variables
        public string Id {
            get => id;
            internal set => id = value;
        }

        public string Text {
            get => text;
            internal set => text = value;
        }

        /// <summary> Position and size in graph space </summary>
        public Rect Rect {
            get => rect;
            internal set => rect = value;
        }
        #endregion
    }
}
