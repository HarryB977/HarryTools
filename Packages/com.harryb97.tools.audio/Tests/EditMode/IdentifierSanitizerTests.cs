using HarryTools.Audio.Editor;
using NUnit.Framework;

namespace HarryTools.Audio.Tests {
    public class IdentifierSanitizerTests {
        [TestCase("wood step 01", "WoodStep01")]
        [TestCase("01-intro", "_01Intro")]
        [TestCase("class", "@class")]
        [TestCase("Ñandú", "Nandu")]
        [TestCase("", "_")]
        [TestCase(null, "_")]
        [TestCase("Wood", "Wood")]
        public void ToIdentifier(string _input, string _expected) {
            Assert.AreEqual(_expected, IdentifierSanitizer.ToIdentifier(_input));
        }
    }
}
