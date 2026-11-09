using NUnit.Framework;
using UnityEngine;

namespace HarryTools.Audio.Tests {
    public class AcousticReverbMappingTests {
        // ==================== VARIABLES ===================
        private GameObject host;
        private AudioReverbFilter filter;

        // ==================== SETUP ===================
        [SetUp]
        public void SetUp() {
            host = new GameObject("Reverb Mapping Test", typeof(AudioSource));
            filter = host.AddComponent<AudioReverbFilter>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(host);

        // ==================== HELPERS ===================
        private static AcousticReverb Reverb(float _wet, float _decay = 2f, float _ratio = 0.8f, float _delay = 0.05f) =>
            new AcousticReverb { Wet = _wet, DecayTime = _decay, HighFrequencyRatio = _ratio, ReflectionsDelay = _delay };

        // ==================== TESTS ===================
        [Test]
        public void MapReverb_LevelMonotonicInWet() {
            float _previous = float.NegativeInfinity;
            float[] _wets = { 0.001f, 0.01f, 0.05f, 0.1f, 0.3f, 0.6f, 1f };
            for (int _i = 0; _i < _wets.Length; _i++) {
                AcousticSimulator.MapReverb(Reverb(_wets[_i]), filter);
                Assert.Greater(filter.reverbLevel, _previous, "Wet " + _wets[_i]);
                _previous = filter.reverbLevel;
            }
        }

        [Test]
        public void MapReverb_WetOne_LevelZero() {
            AcousticSimulator.MapReverb(Reverb(1f), filter);
            Assert.AreEqual(0f, filter.reverbLevel, 0.01f);
            Assert.AreEqual(-600f, filter.reflectionsLevel, 0.01f);
            Assert.AreEqual(0f, filter.dryLevel, 0.01f);
            Assert.AreEqual(0f, filter.room, 0.01f);
            Assert.AreEqual(AudioReverbPreset.User, filter.reverbPreset);
            Assert.AreEqual(2f, filter.decayTime, 0.001f);
            Assert.AreEqual(0.8f, filter.decayHFRatio, 0.001f);
            Assert.AreEqual(0.05f, filter.reflectionsDelay, 0.001f);
            Assert.AreEqual(0.025f, filter.reverbDelay, 0.001f);
        }

        [Test]
        public void MapReverb_ClampsRanges() {
            AcousticSimulator.MapReverb(Reverb(0f, 100f, 9f, 5f), filter);
            Assert.AreEqual(-10000f, filter.reverbLevel, 0.01f);
            Assert.AreEqual(-10000f, filter.reflectionsLevel, 0.01f);
            Assert.AreEqual(20f, filter.decayTime, 0.001f);
            Assert.AreEqual(2f, filter.decayHFRatio, 0.001f);
            Assert.AreEqual(0.3f, filter.reflectionsDelay, 0.001f);
            Assert.AreEqual(0.1f, filter.reverbDelay, 0.001f);

            AcousticSimulator.MapReverb(Reverb(5f, -3f, -1f, -1f), filter);
            Assert.AreEqual(0f, filter.reverbLevel, 0.01f);
            Assert.AreEqual(0.1f, filter.decayTime, 0.001f);
            Assert.AreEqual(0.1f, filter.decayHFRatio, 0.001f);
            Assert.AreEqual(0f, filter.reflectionsDelay, 0.001f);
            Assert.AreEqual(0f, filter.reverbDelay, 0.001f);
        }

        [Test]
        public void Neutral_HasNoReverb() {
            AcousticResult _neutral = AcousticResult.Neutral;
            Assert.IsFalse(_neutral.HasReverb);
            Assert.AreEqual(0f, _neutral.Reverb.Wet);
        }
    }
}
