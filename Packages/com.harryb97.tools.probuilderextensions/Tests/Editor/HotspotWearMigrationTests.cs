using HarryTools.ProBuilderExtensions.Generator;
using NUnit.Framework;
using UnityEngine;

namespace HarryTools.ProBuilderExtensions.Tests {
    public class HotspotWearMigrationTests {
        private const string OldWear = "{\"intensity\":0.4,\"chipSize\":0.08,\"chipDensity\":1}";

        [Test]
        public void OldWear_LoadsAsBites_WithSameLook() {
            var w = JsonUtility.FromJson<HotspotWear>(OldWear);
            Assert.AreEqual(HotspotWearStyle.Bites, w.style);
            Assert.AreEqual(0.08f, w.wearSize, 1e-5f);
            Assert.AreEqual(0f, w.edgeWidth);
            Assert.AreEqual(0.4f * 2f / HotspotWear.BitesDepth, w.intensity, 1e-5f); // (0.5 + 1.5·1) / k
        }

        [Test]
        public void OldWear_IntensityRewriteIsClamped() {
            var w = JsonUtility.FromJson<HotspotWear>("{\"intensity\":0.9,\"chipSize\":0.05,\"chipDensity\":1}");
            Assert.AreEqual(1f, w.intensity);
        }

        [Test]
        public void MigratedWear_SavedAndLoadedAgain_DoesNotMigrateTwice() {
            var once = JsonUtility.FromJson<HotspotWear>(OldWear);
            var twice = JsonUtility.FromJson<HotspotWear>(JsonUtility.ToJson(once));
            Assert.AreEqual(once.intensity, twice.intensity, 1e-6f);
            Assert.AreEqual(HotspotWearStyle.Bites, twice.style);
        }

        [Test]
        public void NewWear_DefaultsToCracksWithARim() {
            var w = new HotspotWear();
            Assert.AreEqual(HotspotWearStyle.Cracks, w.style);
            Assert.AreEqual(0.01f, w.edgeWidth);
        }
    }
}
