using NUnit.Framework;

namespace HarryTools.Audio.Tests {
    public class AudioMathTests {
        [Test] public void LinearToDb_One_IsZero() => Assert.AreEqual(0f, AudioMath.LinearToDb(1f), 0.0001f);
        [Test] public void LinearToDb_Half_IsAboutMinus6() => Assert.AreEqual(-6.02f, AudioMath.LinearToDb(0.5f), 0.05f);
        [Test] public void LinearToDb_Zero_IsMinus80() => Assert.AreEqual(-80f, AudioMath.LinearToDb(0f));
        [Test] public void LinearToDb_Negative_IsMinus80() => Assert.AreEqual(-80f, AudioMath.LinearToDb(-1f));

        [TestCase(0.25f)]
        [TestCase(0.5f)]
        [TestCase(0.75f)]
        public void DbToLinear_RoundTrip(float _linear) => Assert.AreEqual(_linear, AudioMath.DbToLinear(AudioMath.LinearToDb(_linear)), 0.0001f);
    }
}
