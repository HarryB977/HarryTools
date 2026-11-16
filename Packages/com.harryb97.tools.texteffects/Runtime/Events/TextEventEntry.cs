using System;
using HarryTools.Actions;
using UnityEngine;

namespace HarryTools.TextEffects {
    /// <summary> Named text event bound to an action list </summary>
    [Serializable]
    public sealed class TextEventEntry {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private string name;
        [SerializeField] private ActionList actions;
        [Tooltip("Run this event even when the text is skipped instead of typed")]
        [SerializeField] private bool runWhenSkipped = true;
        #endregion

        #region Public Variables
        public string Name => name;
        public ActionList Actions => actions;
        public bool RunWhenSkipped => runWhenSkipped;
        #endregion

        // ==================== CONSTRUCTORS ===================
        public TextEventEntry() { }

        public TextEventEntry(string _name, ActionList _actions, bool _runWhenSkipped = true) {
            name = _name;
            actions = _actions;
            runWhenSkipped = _runWhenSkipped;
        }
    }
}
