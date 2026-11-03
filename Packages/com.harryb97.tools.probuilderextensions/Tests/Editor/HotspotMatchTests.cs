using System.Collections.Generic;
using HarryTools.ProBuilderExtensions.HotspotUV;
using NUnit.Framework;
using UnityEngine;

namespace HarryTools.ProBuilderExtensions.Tests {
    public class HotspotMatchTests {
        private const float AtlasPx = 4096f;
        private const float Tpu = 512f; // 4096 px / 8 m
        private readonly List<Object> created = new();

        [TearDown]
        public void TearDown() {
            foreach (var o in created) Object.DestroyImmediate(o);
            created.Clear();
        }

        /// <summary>Patch at <paramref name="x"/>,<paramref name="y"/> metres with a world size of w×h metres on an 8 m atlas.</summary>
        internal static HotspotPatch Metres(float x, float y, float w, float h) =>
            HotspotPatch.Default(new Rect(x * Tpu / AtlasPx, y * Tpu / AtlasPx, w * Tpu / AtlasPx, h * Tpu / AtlasPx));

        private HotspotTexture LayoutTexture(params HotspotPatch[] patches) {
            var layout = ScriptableObject.CreateInstance<HotspotLayout>();
            layout.patches = new List<HotspotPatch>(patches);
            var hotspot = ScriptableObject.CreateInstance<HotspotTexture>();
            hotspot.layout = layout;
            created.Add(layout);
            created.Add(hotspot);
            return hotspot;
        }

        private static HotspotMatchSettings Settings(int seed = 1) => new() {
            allowRotation = true,
            allowMirror = true,
            randomTolerance = 0f,
            scaleTolerance = 0.25f,
            random = new System.Random(seed),
        };

        [Test]
        public void InTolerance_PicksMatchingPatch() {
            var hotspot = LayoutTexture(Metres(0, 0, 1, 2.7f), Metres(2, 0, 3, 2.7f));
            var match = hotspot.FindBestPatch(new Vector2(1.1f, 2.7f), Settings());
            Assert.AreEqual(0, match.index);
            Assert.IsFalse(match.outOfTolerance);
            Assert.IsFalse(match.rotated);
        }

        [Test]
        public void OutsideTolerance_FallsBackToTilingStrip() {
            var strip = HotspotPatch.Default(new Rect(0, 0.5f, 1, 2.7f * Tpu / AtlasPx));
            strip.allowTiling = true;
            var hotspot = LayoutTexture(Metres(0, 0, 1, 2.7f), Metres(2, 0, 3, 2.7f), strip);
            var match = hotspot.FindBestPatch(new Vector2(6f, 2.7f), Settings());
            Assert.AreEqual(2, match.index);
            Assert.AreEqual(TilingAxis.Horizontal, match.tiling);
            Assert.IsFalse(match.outOfTolerance);
        }

        [Test]
        public void NothingFits_ReturnsNearest_FlagsOutOfTolerance() {
            var hotspot = LayoutTexture(Metres(0, 0, 1, 2.7f), Metres(2, 0, 3, 2.7f));
            var settings = Settings();
            settings.allowRotation = false; // a rotated 1×2.7 patch would score closer than the 3×2.7 one
            var match = hotspot.FindBestPatch(new Vector2(6f, 2.7f), settings);
            Assert.AreEqual(1, match.index);
            Assert.IsTrue(match.outOfTolerance);
        }

        [Test]
        public void RotationRespectsPatchFlag() {
            var wide = Metres(0, 0, 2.7f, 1f);
            wide.allowRotation = false;
            var locked = LayoutTexture(wide).FindBestPatch(new Vector2(1f, 2.7f), Settings());
            Assert.IsFalse(locked.rotated);
            Assert.IsTrue(locked.outOfTolerance);

            wide.allowRotation = true;
            var free = LayoutTexture(wide).FindBestPatch(new Vector2(1f, 2.7f), Settings());
            Assert.IsTrue(free.rotated);
            Assert.IsFalse(free.outOfTolerance);
        }

        [Test]
        public void GlobalRotationSwitch_OverridesPatchFlag() {
            var settings = Settings();
            settings.allowRotation = false;
            var match = LayoutTexture(Metres(0, 0, 2.7f, 1f)).FindBestPatch(new Vector2(1f, 2.7f), settings);
            Assert.IsFalse(match.rotated);
        }

        [Test]
        public void MirrorOnlyWhenAllowed() {
            var plain = LayoutTexture(Metres(0, 0, 1, 1));
            var settings = Settings();
            for (int i = 0; i < 200; i++) {
                var m = plain.FindBestPatch(Vector2.one, settings);
                Assert.IsFalse(m.mirrorX || m.mirrorY);
            }

            var mirrorPatch = Metres(0, 0, 1, 1);
            mirrorPatch.allowMirrorX = true;
            var mirrored = LayoutTexture(mirrorPatch);
            bool seenTrue = false, seenFalse = false;
            for (int i = 0; i < 200; i++) {
                var m = mirrored.FindBestPatch(Vector2.one, settings);
                Assert.IsFalse(m.mirrorY);
                if (m.mirrorX) seenTrue = true; else seenFalse = true;
            }
            Assert.IsTrue(seenTrue && seenFalse);
        }

        [Test]
        public void LegacyRects_BehaveAsBefore() {
            var target = new Texture2D(512, 512);
            var hotspot = ScriptableObject.CreateInstance<HotspotTexture>();
            hotspot.target = target;
            hotspot.texelsPerUnit = 0;
            hotspot.rects = new List<Rect> { new(0, 0, 256, 128), new(256, 0, 128, 256) };
            created.Add(target);
            created.Add(hotspot);

            Assert.AreEqual(2, hotspot.PatchCount);
            Assert.AreEqual(0f, hotspot.EffectiveTexelsPerUnit);
            Assert.IsTrue(hotspot.GetPatch(0).allowRotation);
            Assert.AreEqual(new Rect(0, 0.75f, 0.5f, 0.25f), hotspot.GetUVRect(0));

            var settings = Settings();
            settings.allowRotation = false;
            var wide = hotspot.FindBestPatch(new Vector2(20f, 10f), settings);
            Assert.AreEqual(0, wide.index);
            Assert.IsFalse(wide.outOfTolerance);
            Assert.AreEqual(1, hotspot.FindBestPatch(new Vector2(1f, 2f), settings).index);
        }

        [Test]
        public void DegenerateFace_ReturnsInvalid() {
            var hotspot = LayoutTexture(Metres(0, 0, 1, 1));
            Assert.IsFalse(hotspot.FindBestPatch(new Vector2(0f, 1f), Settings()).IsValid);
            Assert.IsFalse(hotspot.FindBestPatch(new Vector2(float.NaN, 1f), Settings()).IsValid);
        }

        [Test]
        public void NonSquareLayout_UsesItsPixelSize() {
            var hotspot = LayoutTexture(HotspotPatch.Default(new Rect(0, 0, 0.5f, 0.5f)));
            hotspot.layout.aspect = new Vector2Int(2, 1);
            Assert.AreEqual(new Vector2(4096, 2048), hotspot.TextureSize);
            Assert.AreEqual(new Rect(0, 0.5f, 0.5f, 0.5f), hotspot.GetUVRect(0));
            var match = hotspot.FindBestPatch(new Vector2(4f, 2f), Settings());
            Assert.AreEqual(0, match.index);
            Assert.IsFalse(match.outOfTolerance);
        }
        [Test]
        public void LegacyRectsWithDensity_KeepRandomnessAndNeverFlag() {
            var target = new Texture2D(512, 512);
            var hotspot = ScriptableObject.CreateInstance<HotspotTexture>();
            hotspot.target = target;
            hotspot.texelsPerUnit = 256;
            hotspot.rects = new List<Rect> { new(0, 0, 64, 64), new(64, 0, 64, 64), new(128, 0, 64, 64) };
            created.Add(target);
            created.Add(hotspot);

            Assert.AreEqual(0, hotspot.GetPatch(0).inset, "legacy rects must not gain an inset");
            var settings = Settings();
            settings.randomTolerance = 0.15f;
            var seen = new HashSet<int>();
            for (int i = 0; i < 100; i++) {
                var m = hotspot.FindBestPatch(new Vector2(3f, 3f), settings); // far bigger than the 0.25 m rects
                Assert.IsFalse(m.outOfTolerance);
                seen.Add(m.index);
            }
            Assert.AreEqual(3, seen.Count, "re-applying should still pick among equally good rects");
        }
    }
}
