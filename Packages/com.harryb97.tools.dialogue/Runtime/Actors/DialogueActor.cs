using UnityEngine;

namespace HarryTools.Dialogue {
    /// <summary> A scene object that speaks as a character. It is the Self of the actions of the lines of that character </summary>
    [AddComponentMenu("HarryTools/Dialogue/Dialogue Actor")]
    public sealed class DialogueActor : MonoBehaviour {
        // ==================== VARIABLES ===================
        #region Private Variables
        [SerializeField] private DialogueCharacter character;
        [Tooltip("Shows the lines of this actor in ambient dialogues. Empty uses the default ambient container"), SerializeField] private LineContainer container;
        #endregion

        #region Public Variables
        /// <summary> The character this object speaks as </summary>
        public DialogueCharacter Character {
            get => character;
            set => character = value;
        }

        /// <summary> Container of this actor's lines in ambient dialogues, or null to use <see cref="LineContainer.DefaultAmbient"/> </summary>
        public LineContainer Container {
            get => container;
            set => container = value;
        }
        #endregion

        // ==================== UNITY ====================
        #region Unity
        private void OnEnable() {
            DialogueActors.Register(this);
        }

        private void OnDisable() {
            DialogueActors.Unregister(this);
        }
        #endregion
    }
}
