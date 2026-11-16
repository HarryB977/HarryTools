using System;
using NUnit.Framework;

namespace HarryTools.TextEffects.Tests {
    public class TextParserTests {
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
        #region Commands
        [Test]
        public void Plain_PassesThrough() {
            ParsedText result = TextParser.Parse("hola", lookup);
            Assert.AreEqual("hola", result.TmpText);
            Assert.AreEqual("hola", result.Source);
            Assert.AreEqual(0, result.Commands.Count);
            Assert.AreEqual(0, result.Spans.Count);
            Assert.AreEqual(0, result.Issues.Count);
        }

        [Test]
        public void Pause_RemovedAndIndexed() {
            ParsedText result = TextParser.Parse("ab<pause=0.5>cd", lookup);
            Assert.AreEqual("abcd", result.TmpText);
            Assert.AreEqual(1, result.Commands.Count);
            Assert.AreEqual(TextCommandType.Pause, result.Commands[0].Type);
            Assert.AreEqual(2, result.Commands[0].SourceIndex);
            Assert.AreEqual(0.5f, result.Commands[0].Value);
            Assert.AreEqual(-1, result.Commands[0].EventOrdinal);
        }

        [Test]
        public void Speed_Event_Voice_WaitInput() {
            ParsedText result = TextParser.Parse("<speed=2>a<waitinput>b<event=first>c<event=second>d<voice=robot>e", lookup);
            Assert.AreEqual("abcde", result.TmpText);
            Assert.AreEqual(0, result.Issues.Count);
            Assert.AreEqual(5, result.Commands.Count);

            Assert.AreEqual(TextCommandType.Speed, result.Commands[0].Type);
            Assert.AreEqual(2f, result.Commands[0].Value);
            Assert.AreEqual(0, result.Commands[0].SourceIndex);

            Assert.AreEqual(TextCommandType.WaitInput, result.Commands[1].Type);
            Assert.AreEqual(1, result.Commands[1].SourceIndex);

            Assert.AreEqual(TextCommandType.Event, result.Commands[2].Type);
            Assert.AreEqual("first", result.Commands[2].Name);
            Assert.AreEqual(0, result.Commands[2].EventOrdinal);

            Assert.AreEqual(TextCommandType.Event, result.Commands[3].Type);
            Assert.AreEqual("second", result.Commands[3].Name);
            Assert.AreEqual(1, result.Commands[3].EventOrdinal);

            Assert.AreEqual(TextCommandType.Voice, result.Commands[4].Type);
            Assert.AreEqual("robot", result.Commands[4].Name);
            Assert.AreEqual(4, result.Commands[4].SourceIndex);
            Assert.AreEqual(2, result.EventCount);
        }

        [Test]
        public void Voice_Unknown_WarningAndKept() {
            ParsedText result = TextParser.Parse("<voice=ghost>a", lookup);
            Assert.AreEqual("a", result.TmpText);
            Assert.AreEqual(1, result.Commands.Count);
            Assert.AreEqual("ghost", result.Commands[0].Name);
            Assert.AreEqual(1, result.Issues.Count);
            Assert.AreEqual(IssueSeverity.Warning, result.Issues[0].Severity);
        }

        [Test]
        public void Event_InvalidName_ErrorAndRemoved() {
            ParsedText result = TextParser.Parse("<event=a b>", lookup);
            Assert.AreEqual(string.Empty, result.TmpText);
            Assert.AreEqual(0, result.Commands.Count);
            Assert.AreEqual(1, result.Issues.Count);
            Assert.AreEqual(IssueSeverity.Error, result.Issues[0].Severity);
            Assert.AreEqual(0, result.Issues[0].Position);
        }

        [Test]
        public void Pause_Negative_ErrorAndRemoved() {
            ParsedText result = TextParser.Parse("a<pause=-1>b", lookup);
            Assert.AreEqual("ab", result.TmpText);
            Assert.AreEqual(0, result.Commands.Count);
            Assert.AreEqual(1, result.Issues.Count);
            Assert.AreEqual(IssueSeverity.Error, result.Issues[0].Severity);
            Assert.AreEqual(1, result.Issues[0].Position);
        }

        [Test]
        public void Speed_Zero_And_Pause_NotANumber_AreErrors() {
            ParsedText result = TextParser.Parse("<speed=0><pause=abc><pause>", lookup);
            Assert.AreEqual(string.Empty, result.TmpText);
            Assert.AreEqual(0, result.Commands.Count);
            Assert.AreEqual(3, result.Issues.Count);
        }
        #endregion

        #region Ranges
        [Test]
        public void Range_ClosedWithSlash() {
            ParsedText result = TextParser.Parse("x<shake>ab</>y", lookup);
            Assert.AreEqual("xaby", result.TmpText);
            Assert.AreEqual(1, result.Spans.Count);
            Assert.AreEqual(SpanKind.Preset, result.Spans[0].Kind);
            Assert.AreEqual("shake", result.Spans[0].TagName);
            Assert.AreEqual(1, result.Spans[0].Start);
            Assert.AreEqual(3, result.Spans[0].End);
        }

        [Test]
        public void Range_ClosedByName_ClosesNested() {
            ParsedText result = TextParser.Parse("<shake><fade>ab</shake>c", lookup);
            Assert.AreEqual("abc", result.TmpText);
            Assert.AreEqual(2, result.Spans.Count);
            Assert.AreEqual("shake", result.Spans[0].TagName);
            Assert.AreEqual(0, result.Spans[0].Start);
            Assert.AreEqual(2, result.Spans[0].End);
            Assert.AreEqual("fade", result.Spans[1].TagName);
            Assert.AreEqual(0, result.Spans[1].Start);
            Assert.AreEqual(2, result.Spans[1].End);
        }

        [Test]
        public void Range_ClosedByName_LeavesOuterOpen() {
            ParsedText result = TextParser.Parse("<shake>a<fade>b</fade>c", lookup);
            Assert.AreEqual("abc", result.TmpText);
            Assert.AreEqual(1, result.Spans[1].Start);
            Assert.AreEqual(2, result.Spans[1].End);
            Assert.AreEqual(3, result.Spans[0].End);
        }

        [Test]
        public void Range_Unclosed_RunsToEnd() {
            ParsedText result = TextParser.Parse("<shake>abc", lookup);
            Assert.AreEqual("abc", result.TmpText);
            Assert.AreEqual(0, result.Spans[0].Start);
            Assert.AreEqual(3, result.Spans[0].End);
        }

        [Test]
        public void Range_Overrides() {
            ParsedText result = TextParser.Parse("<shake amp=3 freq=\"2\">a</>", lookup);
            Assert.AreEqual("a", result.TmpText);
            Assert.AreEqual(1, result.Spans.Count);
            Assert.AreEqual(2, result.Spans[0].Overrides.Count);
            Assert.AreEqual("amp", result.Spans[0].Overrides[0].Name);
            Assert.AreEqual("3", result.Spans[0].Overrides[0].Value);
            Assert.AreEqual("freq", result.Spans[0].Overrides[1].Name);
            Assert.AreEqual("2", result.Spans[0].Overrides[1].Value);
        }

        [Test]
        public void Range_MalformedOverrides_ErrorAndRemoved() {
            ParsedText result = TextParser.Parse("<shake amp>a</>", lookup);
            Assert.AreEqual("a", result.TmpText);
            Assert.AreEqual(0, result.Spans.Count);
            Assert.AreEqual(2, result.Issues.Count);
            Assert.AreEqual(IssueSeverity.Error, result.Issues[0].Severity);
            Assert.AreEqual(IssueSeverity.Warning, result.Issues[1].Severity);
        }

        [Test]
        public void Instant_Span() {
            ParsedText result = TextParser.Parse("<instant>ab</>c", lookup);
            Assert.AreEqual("abc", result.TmpText);
            Assert.AreEqual(SpanKind.Instant, result.Spans[0].Kind);
            Assert.AreEqual(0, result.Spans[0].Start);
            Assert.AreEqual(2, result.Spans[0].End);
        }

        [Test]
        public void Close_NothingOpen_WarningAndRemoved() {
            ParsedText result = TextParser.Parse("a</>b", lookup);
            Assert.AreEqual("ab", result.TmpText);
            Assert.AreEqual(1, result.Issues.Count);
            Assert.AreEqual(IssueSeverity.Warning, result.Issues[0].Severity);
            Assert.AreEqual(1, result.Issues[0].Position);
        }

        [Test]
        public void CloseByName_NothingOpen_WarningAndRemoved() {
            ParsedText result = TextParser.Parse("a</shake>b", lookup);
            Assert.AreEqual("ab", result.TmpText);
            Assert.AreEqual(1, result.Issues.Count);
            Assert.AreEqual(IssueSeverity.Warning, result.Issues[0].Severity);
        }
        #endregion

        #region TextMeshPro Tags
        [Test]
        public void TmpTags_Untouched() {
            ParsedText result = TextParser.Parse("<b><shake>a</></b>", lookup);
            Assert.AreEqual("<b>a</b>", result.TmpText);
            Assert.AreEqual(1, result.Spans.Count);
            Assert.AreEqual(3, result.Spans[0].Start);
            Assert.AreEqual(4, result.Spans[0].End);
        }

        [Test]
        public void CloseByName_TmpTag_NotConsumed() {
            ParsedText result = TextParser.Parse("<b>a</b>", lookup);
            Assert.AreEqual("<b>a</b>", result.TmpText);
            Assert.AreEqual(0, result.Issues.Count);
        }

        [Test]
        public void NoParse_NotConsumed() {
            const string text = "<noparse><pause=1></noparse>";
            ParsedText result = TextParser.Parse(text, lookup);
            Assert.AreEqual(text, result.TmpText);
            Assert.AreEqual(0, result.Commands.Count);
            Assert.AreEqual(0, result.Issues.Count);
        }

        [Test]
        public void NoParse_ThenTagsParsedAgain() {
            ParsedText result = TextParser.Parse("<noparse><pause=1></noparse><pause=2>a", lookup);
            Assert.AreEqual("<noparse><pause=1></noparse>a", result.TmpText);
            Assert.AreEqual(1, result.Commands.Count);
            Assert.AreEqual(2f, result.Commands[0].Value);
            Assert.AreEqual("<noparse><pause=1></noparse>".Length, result.Commands[0].SourceIndex);
        }

        [Test]
        public void UnknownTag_LeftForTmp() {
            ParsedText result = TextParser.Parse("<foo>a", lookup);
            Assert.AreEqual("<foo>a", result.TmpText);
            Assert.AreEqual(0, result.Issues.Count);
            Assert.AreEqual(0, result.Spans.Count);
        }

        [Test]
        public void LooseAngleBrackets_LeftAlone() {
            ParsedText result = TextParser.Parse("a < b <shake>c</> > d <", lookup);
            Assert.AreEqual("a < b c > d <", result.TmpText);
            Assert.AreEqual(1, result.Spans.Count);
        }
        #endregion

        #region Misc
        [Test]
        public void CaseInsensitive() {
            ParsedText result = TextParser.Parse("<PAUSE=1><Shake>a</SHAKE>", lookup);
            Assert.AreEqual("a", result.TmpText);
            Assert.AreEqual(1, result.Commands.Count);
            Assert.AreEqual(TextCommandType.Pause, result.Commands[0].Type);
            Assert.AreEqual(1, result.Spans.Count);
            Assert.AreEqual(0, result.Spans[0].Start);
            Assert.AreEqual(1, result.Spans[0].End);
            Assert.AreEqual(0, result.Issues.Count);
        }

        [Test]
        public void Pause_UsesInvariantCulture() {
            System.Globalization.CultureInfo previous = System.Globalization.CultureInfo.CurrentCulture;
            try {
                System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("es-ES");
                ParsedText result = TextParser.Parse("<pause=1.5>", lookup);
                Assert.AreEqual(1.5f, result.Commands[0].Value);
            } finally {
                System.Globalization.CultureInfo.CurrentCulture = previous;
            }
        }

        [Test]
        public void NullText_IsEmpty() {
            ParsedText result = TextParser.Parse(null, lookup);
            Assert.AreEqual(string.Empty, result.TmpText);
        }

        [Test]
        public void Reuse_IntoSameInstance_Clears() {
            ParsedText into = new ParsedText();
            TextParser.Parse("<shake>a<event=x><pause=1>b</shake></>", lookup, into);
            Assert.AreEqual(1, into.Spans.Count);
            Assert.AreEqual(2, into.Commands.Count);
            Assert.AreEqual(1, into.Issues.Count);
            Assert.AreEqual(1, into.EventCount);

            TextParser.Parse("plain", lookup, into);
            Assert.AreEqual("plain", into.TmpText);
            Assert.AreEqual("plain", into.Source);
            Assert.AreEqual(0, into.Spans.Count);
            Assert.AreEqual(0, into.Commands.Count);
            Assert.AreEqual(0, into.Issues.Count);
            Assert.AreEqual(0, into.EventCount);
        }

        [Test]
        public void ReservedTags_Contains_OwnAndTmpNames() {
            Assert.IsTrue(ReservedTags.IsReserved("pause"));
            Assert.IsTrue(ReservedTags.IsReserved("INSTANT"));
            Assert.IsTrue(ReservedTags.IsReserved("b"));
            Assert.IsTrue(ReservedTags.IsReserved("font-weight"));
            Assert.IsFalse(ReservedTags.IsReserved("shake"));
            Assert.IsFalse(ReservedTags.IsReserved(null));
            Assert.AreEqual(44, ReservedTags.Names.Count);
        }
        #endregion
    }
}
