using System;
using System.Collections.Generic;
using HarryTools.TextEffects.Editor;
using NUnit.Framework;

namespace HarryTools.TextEffects.Tests {
    public class TextValidatorTests {
        // ==================== VARIABLES ===================
        #region Private Variables
        private sealed class FakeLookup : ITagLookup {
            public bool IsPreset(string _name) {
                return string.Equals(_name, "shake", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(_name, "fade", StringComparison.OrdinalIgnoreCase);
            }

            public bool IsVoice(string _name) {
                return string.Equals(_name, "robot", StringComparison.OrdinalIgnoreCase);
            }
        }

        private static readonly ITagLookup lookup = new FakeLookup();
        #endregion

        // ==================== TESTS ====================
        #region Single Text
        private static List<TextIssue> Validate(string _text, Func<string, bool> _eventExists = null) {
            List<TextIssue> issues = new List<TextIssue>();
            TextValidator.ValidateText(_text, lookup, _eventExists, issues);
            return issues;
        }

        [Test]
        public void Validate_UnknownVoice_Error() {
            List<TextIssue> issues = Validate("<voice=ghost>hi");
            Assert.AreEqual(1, issues.Count);
            Assert.AreEqual(IssueSeverity.Error, issues[0].Severity);
            StringAssert.Contains("ghost", issues[0].Message);
        }

        [Test]
        public void Validate_KnownVoice_NoIssue() {
            Assert.AreEqual(0, Validate("<voice=robot>hi").Count);
        }

        [Test]
        public void Validate_EventNotInTable_Warning() {
            List<TextIssue> issues = Validate("a<event=boom>b", _name => false);
            Assert.AreEqual(1, issues.Count);
            Assert.AreEqual(IssueSeverity.Warning, issues[0].Severity);
            StringAssert.Contains("boom", issues[0].Message);
        }

        [Test]
        public void Validate_EventInTable_NoIssue() {
            Assert.AreEqual(0, Validate("a<event=boom>b", _name => true).Count);
        }

        [Test]
        public void Validate_NoEventLookup_NoEventWarning() {
            Assert.AreEqual(0, Validate("a<event=boom>b").Count);
        }

        [Test]
        public void Validate_LooksLikePreset_Warning() {
            List<TextIssue> issues = Validate("<shaek>a</>");
            TextIssue found = issues.Find(_issue => _issue.Message.Contains("shaek"));
            Assert.IsNotNull(found.Message);
            Assert.AreEqual(IssueSeverity.Warning, found.Severity);
            StringAssert.Contains("Unknown effect preset", found.Message);
        }

        [Test]
        public void Validate_UnknownTagWithParams_Warning() {
            Assert.AreEqual(1, Validate("<shaek amp=3>a").Count);
        }

        [Test]
        public void Validate_TmpTag_NoWarning() {
            Assert.AreEqual(0, Validate("<b>a</b>").Count);
        }

        [Test]
        public void Validate_RegisteredPreset_NoWarning() {
            Assert.AreEqual(0, Validate("<shake>a</>").Count);
        }
        #endregion

        #region Locales
        private static List<LocaleIssue> ValidateLocales(params string[] _pairs) {
            Dictionary<string, string> texts = new Dictionary<string, string>();
            for (int i = 0; i < _pairs.Length; i += 2) texts[_pairs[i]] = _pairs[i + 1];
            List<LocaleIssue> issues = new List<LocaleIssue>();
            TextValidator.ValidateLocales("en", texts, lookup, issues);
            return issues;
        }

        [Test]
        public void Locales_Identical_NoIssues() {
            Assert.AreEqual(0, ValidateLocales("en", "a<event=x><pause=1><shake>b</>", "es", "c<event=x><pause=2><shake>d</>").Count);
        }

        [Test]
        public void Locales_MissingEvent_Error() {
            List<LocaleIssue> issues = ValidateLocales("en", "a<event=x>b", "es", "ab");
            Assert.AreEqual(1, issues.Count);
            Assert.AreEqual("es", issues[0].Locale);
            Assert.AreEqual(IssueSeverity.Error, issues[0].Issue.Severity);
        }

        [Test]
        public void Locales_ExtraWaitInput_Error() {
            List<LocaleIssue> issues = ValidateLocales("en", "ab", "es", "a<waitinput>b");
            Assert.AreEqual(1, issues.Count);
            Assert.AreEqual(IssueSeverity.Error, issues[0].Issue.Severity);
        }

        [Test]
        public void Locales_DifferentPauseCount_Warning() {
            List<LocaleIssue> issues = ValidateLocales("en", "a<pause=1>b", "es", "ab");
            Assert.AreEqual(1, issues.Count);
            Assert.AreEqual(IssueSeverity.Warning, issues[0].Issue.Severity);
        }

        [Test]
        public void Locales_DifferentPresets_Warning() {
            List<LocaleIssue> issues = ValidateLocales("en", "<shake>a</>", "es", "<fade>a</>");
            Assert.AreEqual(1, issues.Count);
            Assert.AreEqual(IssueSeverity.Warning, issues[0].Issue.Severity);
        }

        [Test]
        public void Locales_EmptyTranslation_Skipped() {
            Assert.AreEqual(0, ValidateLocales("en", "a<event=x><waitinput>b", "es", "", "fr", null).Count);
        }

        [Test]
        public void Locales_ErrorInTranslation_ReportedWithLocale() {
            List<LocaleIssue> issues = ValidateLocales("en", "ab", "es", "<voice=ghost>ab");
            Assert.AreEqual(1, issues.Count);
            Assert.AreEqual("es", issues[0].Locale);
        }
        #endregion
    }
}
