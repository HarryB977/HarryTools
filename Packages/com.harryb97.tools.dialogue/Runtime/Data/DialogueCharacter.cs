using HarryTools.TextEffects;
using UnityEngine;
using UnityEngine.Localization;

namespace HarryTools.Dialogue {
    /// <summary> A speaker: the name shown on its lines and the voice they are typed with </summary>
    [CreateAssetMenu(fileName = "NewCharacter", menuName = "HarryTools/Dialogue/Character")]
    public sealed class DialogueCharacter : ScriptableObject {
        [SerializeField] private LocalizedString displayName = new LocalizedString();
        [SerializeField] private TypingVoice typingVoice;

        public LocalizedString DisplayName => displayName;
        public TypingVoice TypingVoice => typingVoice;
    }
}
