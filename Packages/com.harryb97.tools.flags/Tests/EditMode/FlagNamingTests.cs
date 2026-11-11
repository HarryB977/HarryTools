using NUnit.Framework;

namespace HarryTools.Flags.Tests
{
    public class FlagNamingTests
    {
        [TestCase("Door01", true)]
        [TestCase("door_01", true)]
        [TestCase("Door 01", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        [TestCase("Puerta-1", false)]
        [TestCase("Ñandú", false)]
        public void IsValidName(string _name, bool _ok) => Assert.AreEqual(_ok, FlagNaming.IsValidName(_name));

        [Test]
        public void NewGuid_Is32HexCharacters()
        {
            string _guid = FlagNaming.NewGuid();
            Assert.AreEqual(32, _guid.Length);
            Assert.IsTrue(System.Text.RegularExpressions.Regex.IsMatch(_guid, "^[0-9a-f]{32}$"));
        }

        [Test]
        public void Comparer_IgnoresCase() => Assert.IsTrue(FlagNaming.Comparer.Equals("Door01", "door01"));
    }
}
