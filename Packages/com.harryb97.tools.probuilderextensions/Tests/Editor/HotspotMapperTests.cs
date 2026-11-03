using System.Collections.Generic;
using HarryTools.ProBuilderExtensions.HotspotUV;
using HarryTools.ProBuilderExtensions.HotspotUV.Editor;
using NUnit.Framework;
using UnityEngine;

namespace HarryTools.ProBuilderExtensions.Tests {
    public class HotspotMapperTests {
        private const float Eps = 1e-5f;
        private readonly List<Object> created = new();

        [TearDown]
        public void TearDown() {
            foreach (var o in created) Object.DestroyImmediate(o);
            created.Clear();
        }

        private HotspotTexture Hotspot(int resolution, HotspotPatch patch) {
            var layout = ScriptableObject.CreateInstance<HotspotLayout>();
            layout.recommendedResolution = resolution;
            layout.patches = new List<HotspotPatch> { patch };
            var hotspot = ScriptableObject.CreateInstance<HotspotTexture>();
            hotspot.layout = layout;
            created.Add(layout);
            created.Add(hotspot);
            return hotspot;
        }

        private static HotspotPatch Patch(Rect rect, int inset = 0) {
            var p = HotspotPatch.Default(rect);
            p.inset = inset;
            return p;
        }

        private static Vector2[] Quad(float w, float h) => new[] {
            new Vector2(0, 0), new Vector2(w, 0), new Vector2(w, h), new Vector2(0, h),
        };

        private static void AssertUV(Vector2 expected, Vector2 actual) {
            Assert.AreEqual(expected.x, actual.x, Eps, $"u of {actual}");
            Assert.AreEqual(expected.y, actual.y, Eps, $"v of {actual}");
        }

        private static Vector2[] Map(HotspotTexture hotspot, Vector2[] points, HotspotMatch match) {
            var uvs = new Vector2[points.Length];
            Assert.IsTrue(HotspotMapper.Map(points, hotspot, match, false, uvs));
            return uvs;
        }

        [Test]
        public void FillsRect() {
            var hotspot = Hotspot(1024, Patch(new Rect(0.25f, 0.25f, 0.5f, 0.5f)));
            var uvs = Map(hotspot, Quad(2, 1), new HotspotMatch());
            AssertUV(new Vector2(0.25f, 0.25f), uvs[0]);
            AssertUV(new Vector2(0.75f, 0.25f), uvs[1]);
            AssertUV(new Vector2(0.75f, 0.75f), uvs[2]);
            AssertUV(new Vector2(0.25f, 0.75f), uvs[3]);
        }

        [Test]
        public void Inset_ShrinksByTexels() {
            var hotspot = Hotspot(1024, Patch(new Rect(0.25f, 0.25f, 0.5f, 0.5f), inset: 2));
            var uvs = Map(hotspot, Quad(2, 1), new HotspotMatch());
            const float t = 2f / 1024f;
            AssertUV(new Vector2(0.25f + t, 0.25f + t), uvs[0]);
            AssertUV(new Vector2(0.75f - t, 0.75f - t), uvs[2]);
        }

        [Test]
        public void MirrorX_FlipsWithinRect() {
            var hotspot = Hotspot(1024, Patch(new Rect(0.25f, 0.25f, 0.5f, 0.5f)));
            var uvs = Map(hotspot, Quad(2, 1), new HotspotMatch { mirrorX = true });
            AssertUV(new Vector2(0.75f, 0.25f), uvs[0]);
            AssertUV(new Vector2(0.25f, 0.25f), uvs[1]);
            AssertUV(new Vector2(0.25f, 0.75f), uvs[2]);
        }

        [Test]
        public void Rotated_SwapsAxes() {
            var hotspot = Hotspot(1024, Patch(new Rect(0.25f, 0.25f, 0.5f, 0.5f)));
            var uvs = Map(hotspot, Quad(2, 1), new HotspotMatch { rotated = true });
            foreach (var uv in uvs) {
                Assert.That(uv.x, Is.InRange(0.25f - Eps, 0.75f + Eps));
                Assert.That(uv.y, Is.InRange(0.25f - Eps, 0.75f + Eps));
            }
            // The face's long side (x) now runs along v.
            Assert.AreEqual(0f, uvs[1].x - uvs[0].x, Eps);
            Assert.AreEqual(0.5f, Mathf.Abs(uvs[1].y - uvs[0].y), Eps);
        }

        [Test]
        public void TilingHorizontal_RepeatsByWorldLength() {
            var strip = Patch(new Rect(0, 0.5f, 1, 0.1f));
            strip.allowTiling = true;
            var hotspot = Hotspot(4096, strip); // 8 m wide → 512 px/m
            var uvs = Map(hotspot, Quad(16, 1), new HotspotMatch { tiling = TilingAxis.Horizontal });
            AssertUV(new Vector2(0f, 0.4f), uvs[0]);
            AssertUV(new Vector2(2f, 0.4f), uvs[1]);
            AssertUV(new Vector2(2f, 0.5f), uvs[2]);
        }

        [Test]
        public void Degenerate_ReturnsFalse() {
            var hotspot = Hotspot(1024, Patch(new Rect(0.25f, 0.25f, 0.5f, 0.5f)));
            var points = new[] { Vector2.one, Vector2.one, Vector2.one };
            var uvs = new[] { new Vector2(7, 7), new Vector2(7, 7), new Vector2(7, 7) };
            Assert.IsFalse(HotspotMapper.Map(points, hotspot, new HotspotMatch(), false, uvs));
            foreach (var uv in uvs) Assert.AreEqual(new Vector2(7, 7), uv);
        }
    }
}
