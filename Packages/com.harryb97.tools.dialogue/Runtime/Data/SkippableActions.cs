using System;
using HarryTools.Actions;
using UnityEngine;

namespace HarryTools.Dialogue {
    /// <summary> A list of actions plus whether it still runs when the dialogue is skipped </summary>
    [Serializable]
    public sealed class SkippableActions {
        [SerializeField] private ActionList actions = new ActionList();
        [Tooltip("The actions still run when the dialogue is skipped"), SerializeField] private bool runWhenSkipped = true;

        public ActionList Actions {
            get => actions;
            internal set => actions = value;
        }

        public bool RunWhenSkipped {
            get => runWhenSkipped;
            internal set => runWhenSkipped = value;
        }
    }
}
