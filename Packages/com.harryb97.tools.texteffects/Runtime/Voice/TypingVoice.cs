using UnityEngine;
#if HARRYTOOLS_TEXTEFFECTS_AUDIO
using HarryTools.Audio;
#endif

namespace HarryTools.TextEffects {
    /// <summary> Typing sound settings: which characters sound, how often, and the pitch variation </summary>
    [CreateAssetMenu(menuName = "HarryTools/Text Effects/Typing Voice", fileName = "Typing Voice")]
    public sealed class TypingVoice : ScriptableObject {
        // ==================== VARIABLES ===================
        #region Public Variables
        public string TagName {
            get => tagName;
            internal set => tagName = value;
        }

        public int EveryNCharacters {
            get => everyNCharacters;
            internal set => everyNCharacters = Mathf.Max(1, value);
        }

        public bool SkipWhitespace => skipWhitespace;
        public bool SkipPunctuation => skipPunctuation;
        public Vector2 PitchRange => pitchRange;

#if HARRYTOOLS_TEXTEFFECTS_AUDIO
        public SoundRef Sound => sound;
#endif
        #endregion

        #region Private Variables
        [Tooltip("Name used in the markup to switch to this voice"), SerializeField]
        private string tagName;

        [Tooltip("A sound is played every N soundable characters"), SerializeField, Min(1)]
        private int everyNCharacters = 1;

        [Tooltip("Whitespace characters do not sound"), SerializeField]
        private bool skipWhitespace = true;

        [Tooltip("Punctuation characters do not sound"), SerializeField]
        private bool skipPunctuation = true;

        [Tooltip("Random pitch multiplier range (min, max)"), SerializeField]
        private Vector2 pitchRange = new Vector2(0.95f, 1.05f);

#if HARRYTOOLS_TEXTEFFECTS_AUDIO
        [Tooltip("Sound played for each typed character"), SerializeField]
        private SoundRef sound;
#endif
        #endregion

        // ==================== METHODS ====================
        #region Public Methods
        /// <summary> False for the characters this voice is set to skip </summary>
        public bool CanSound(char _c) {
            if (skipWhitespace && char.IsWhiteSpace(_c)) return false;
            if (skipPunctuation && char.IsPunctuation(_c)) return false;
            return true;
        }

        /// <summary> True when the soundable character at this position (counted from 0 since the last reset) should play </summary>
        public bool ShouldPlay(int _soundableCountSinceReset) {
            int _n = Mathf.Max(1, everyNCharacters);
            return _soundableCountSinceReset % _n == 0;
        }
        #endregion

        #region Unity Callbacks
        private void OnValidate() {
            everyNCharacters = Mathf.Max(1, everyNCharacters);
            if (pitchRange.y < pitchRange.x) pitchRange.y = pitchRange.x;
        }
        #endregion
    }
}
