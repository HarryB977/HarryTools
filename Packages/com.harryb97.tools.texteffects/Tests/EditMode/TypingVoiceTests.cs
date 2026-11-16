using NUnit.Framework;
using UnityEngine;

namespace HarryTools.TextEffects.Tests {
    public class TypingVoiceTests {
        // ==================== TESTS ====================
        #region Tests
        [Test]
        public void Voice_SkipsWhitespaceAndPunctuation() {
            TypingVoice _voice = ScriptableObject.CreateInstance<TypingVoice>();
            try {
                foreach (char _c in new[] { ' ', '\n', '.', '!', '¿' }) Assert.IsFalse(_voice.CanSound(_c), $"'{_c}' should not sound");
                foreach (char _c in new[] { 'a', 'Ñ', '3' }) Assert.IsTrue(_voice.CanSound(_c), $"'{_c}' should sound");
            } finally {
                Object.DestroyImmediate(_voice);
            }
        }

        [Test]
        public void Voice_EveryN() {
            TypingVoice _voice = ScriptableObject.CreateInstance<TypingVoice>();
            try {
                _voice.EveryNCharacters = 3;
                Assert.IsTrue(_voice.ShouldPlay(0));
                Assert.IsFalse(_voice.ShouldPlay(1));
                Assert.IsFalse(_voice.ShouldPlay(2));
                Assert.IsTrue(_voice.ShouldPlay(3));
            } finally {
                Object.DestroyImmediate(_voice);
            }
        }
        #endregion
    }
}
