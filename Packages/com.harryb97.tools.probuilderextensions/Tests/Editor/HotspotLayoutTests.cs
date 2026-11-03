using System.Collections.Generic;
using HarryTools.ProBuilderExtensions.HotspotUV;
using NUnit.Framework;
using UnityEngine;

namespace HarryTools.ProBuilderExtensions.Tests {
    public class HotspotLayoutTests {
        private readonly List<Object> created = new();

        [TearDown]
        public void TearDown() {
            foreach (var o in created) Object.DestroyImmediate(o);
            created.Clear();
        }

        private HotspotLayout Layout(params HotspotPatch[] patches) {
            var layout = ScriptableObject.CreateInstance<HotspotLayout>();
            layout.patches = new List<HotspotPatch>(patches);
            created.Add(layout);
            return layout;
        }

        private static HotspotPatch Tiling(Rect r) {
            var p = HotspotPatch.Default(r);
            p.allowTiling = true;
            return p;
        }

        [Test]
        public void Default_HasExpectedFlags() {
            var p = HotspotPatch.Default(new Rect(0, 0, 1, 1));
            Assert.IsTrue(p.allowRotation);
            Assert.IsFalse(p.allowMirrorX);
            Assert.IsFalse(p.allowMirrorY);
            Assert.IsFalse(p.allowTiling);
            Assert.AreEqual(1, p.inset);
        }

        [Test]
        public void GetPixelRect_ScalesNormalizedRect() {
            var layout = Layout(HotspotPatch.Default(new Rect(0.25f, 0.5f, 0.5f, 0.25f)));
            Assert.AreEqual(new Rect(1024, 2048, 2048, 1024), layout.GetPixelRect(0, new Vector2(4096, 4096)));
        }

        [Test]
        public void GetTextureSize_UsesAspect() {
            var layout = Layout();
            layout.aspect = new Vector2Int(2, 1);
            Assert.AreEqual(new Vector2Int(4096, 2048), layout.GetTextureSize(4096));
        }

        [Test]
        public void GetTexelsPerUnit_DividesWidthByWorldWidth() {
            var layout = Layout();
            Assert.AreEqual(8f, layout.worldWidth);
            Assert.AreEqual(512f, layout.GetTexelsPerUnit(4096));
            Assert.AreEqual(256f, layout.GetTexelsPerUnit(2048));
        }

        [Test]
        public void GetTilingAxis_DependsOnFullSpanAndFlag() {
            var layout = Layout(
                Tiling(new Rect(0, 0.2f, 1, 0.1f)),
                Tiling(new Rect(0.3f, 0, 0.1f, 1)),
                Tiling(new Rect(0.1f, 0.2f, 0.5f, 0.1f)),
                HotspotPatch.Default(new Rect(0, 0.2f, 1, 0.1f)));
            Assert.AreEqual(TilingAxis.Horizontal, layout.GetTilingAxis(0));
            Assert.AreEqual(TilingAxis.Vertical, layout.GetTilingAxis(1));
            Assert.AreEqual(TilingAxis.None, layout.GetTilingAxis(2));
            Assert.AreEqual(TilingAxis.None, layout.GetTilingAxis(3));
        }

        [Test]
        public void Validate_ValidLayout_ReturnsTrue() {
            var layout = Layout(HotspotPatch.Default(new Rect(0, 0, 0.5f, 0.5f)), Tiling(new Rect(0, 0.6f, 1, 0.1f)));
            var errors = new List<string>();
            Assert.IsTrue(layout.Validate(errors));
            CollectionAssert.IsEmpty(errors);
        }

        [Test]
        public void Validate_OutOfBounds_ReportsOneError() {
            AssertSingleError(Layout(HotspotPatch.Default(new Rect(0.8f, 0, 0.5f, 0.5f))));
        }

        [Test]
        public void Validate_TilingNotFullSpan_ReportsOneError() {
            AssertSingleError(Layout(Tiling(new Rect(0.1f, 0, 0.5f, 0.5f))));
        }

        [Test]
        public void Validate_ZeroSize_ReportsOneError() {
            AssertSingleError(Layout(HotspotPatch.Default(new Rect(0.1f, 0.1f, 0, 0.5f))));
        }

        private static void AssertSingleError(HotspotLayout layout) {
            var errors = new List<string>();
            Assert.IsFalse(layout.Validate(errors));
            Assert.AreEqual(1, errors.Count, string.Join("\n", errors));
        }
    }
}
