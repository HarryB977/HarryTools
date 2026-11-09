using NUnit.Framework;
using UnityEngine;

namespace HarryTools.Audio.Tests {
    public class SpatialGainTests {
        // ==================== HELPERS ===================
        private static SpatialSettings Make(AudioRolloffMode _rolloff, float _min = 2f, float _max = 10f) {
            SpatialSettings _spatial = SpatialSettings.Default3D;
            _spatial.Rolloff = _rolloff;
            _spatial.MinDistance = _min;
            _spatial.MaxDistance = _max;
            return _spatial;
        }

        // ==================== GAIN ===================
        [Test]
        public void GainAt_MatchesRolloffModes() {
            SpatialSettings _log = Make(AudioRolloffMode.Logarithmic);
            Assert.AreEqual(0.5f, _log.GainAt(4f), 0.0001f);
            Assert.AreEqual(0.2f, _log.GainAt(10f), 0.0001f);
            Assert.AreEqual(0.2f, _log.GainAt(50f), 0.0001f, "Distance is clamped to MaxDistance.");

            SpatialSettings _linear = Make(AudioRolloffMode.Linear);
            Assert.AreEqual(0.5f, _linear.GainAt(6f), 0.0001f);
            Assert.AreEqual(0f, _linear.GainAt(10f), 0.0001f);

            SpatialSettings _custom = Make(AudioRolloffMode.Custom);
            _custom.CustomRolloff = AnimationCurve.Linear(0f, 1f, 1f, 0.2f);
            Assert.AreEqual(0.6f, _custom.GainAt(5f), 0.0001f);

            SpatialSettings _noCurve = Make(AudioRolloffMode.Custom);
            Assert.AreEqual(0.5f, _noCurve.GainAt(4f), 0.0001f, "A custom rolloff without keys falls back to logarithmic.");

            SpatialSettings _half = Make(AudioRolloffMode.Logarithmic);
            _half.SpatialBlend = 0.5f;
            Assert.AreEqual(0.75f, _half.GainAt(4f), 0.0001f);
        }

        [Test]
        public void GainAt_BelowMinDistance_IsOne() {
            Assert.AreEqual(1f, Make(AudioRolloffMode.Logarithmic).GainAt(1f), 0.0001f);
            Assert.AreEqual(1f, Make(AudioRolloffMode.Linear).GainAt(0f), 0.0001f);
        }

        [Test]
        public void GainAt_2D_IsOne() {
            Assert.AreEqual(1f, SpatialSettings.Default2D.GainAt(40f), 0.0001f);
        }
    }
}
