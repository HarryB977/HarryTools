using System;
using UnityEngine;

namespace HarryTools.Dialogue {
    /// <summary> Fixes which actor speaks a character in one dialogue run, ahead of the registered actors </summary>
    [Serializable]
    public sealed class ActorBinding {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private DialogueCharacter character;
        [SerializeField] private DialogueActor actor;
        #endregion

        #region Public Variables
        public DialogueCharacter Character {
            get => character;
            set => character = value;
        }

        public DialogueActor Actor {
            get => actor;
            set => actor = value;
        }
        #endregion

        // ==================== START ====================
        public ActorBinding() { }

        public ActorBinding(DialogueCharacter _character, DialogueActor _actor) {
            character = _character;
            actor = _actor;
        }
    }
}
