using HarryTools.TextEffects;
using UnityEngine;
using UnityEngine.Localization;

namespace HarryTools.Dialogue {
    /// <summary> A speaker: the name shown on its lines and the voice they are typed with </summary>
    [CreateAssetMenu(fileName = "NewCharacter", menuName = "HarryTools/Dialogue/Character")]
    public sealed class DialogueCharacter : ScriptableObject {
        [SerializeField] private LocalizedString displayName = new LocalizedString();
        [SerializeField] private TypingVoice typingVoice;
        [SerializeField, HideInInspector] private string characterId;

        public LocalizedString DisplayName => displayName;
        public TypingVoice TypingVoice => typingVoice;

        /// <summary> Stable id of the character, the key of its name in the characters string table. Written by the editor on import; empty on an instance created in code </summary>
        public string CharacterId {
            get => characterId;
            internal set => characterId = value;
        }
    }
}
