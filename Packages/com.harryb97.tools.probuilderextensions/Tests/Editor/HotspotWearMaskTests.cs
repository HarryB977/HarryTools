using System;
using System.Collections.Generic;
using HarryTools.ProBuilderExtensions.Generator;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HarryTools.ProBuilderExtensions.Tests {
    public class HotspotWearMaskTests {
        private readonly List<Object> created = new();

        [TearDown]
        public void TearDown() {
            foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
        }

        /// <summary>A non-readable texture, black where <paramref name="black"/> is true and white elsewhere.</summary>
        private Texture2D Source(int w, int h, Func<int, int, bool> black) {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) pixels[y * w + x] = black(x, y) ? Color.black : Color.white;
            tex.SetPixels(pixels);
            tex.Apply(false, true);
            created.Add(tex);
            return tex;
        }

        private Texture2D Build(Texture2D source, float border) {
            var mask = HotspotWearMask.Build(source, border);
            created.Add(mask);
            return mask;
        }

        [Test]
        public void Build_KeepsDarkConnectedToTheRim_DropsSpecks() {
            // Strip: 8 columns; rows 8..23 (the frame corners, 8 rows at each end, are skipped).
            var m = Build(Source(32, 32, (x, y) => x < 3 || (y == 13 && x < 7) || (x == 5 && y == 16)), 0.25f);
            Assert.AreEqual(8, m.width);
            Assert.AreEqual(16, m.height);
            Assert.Greater(m.GetPixel(1, 12).r, 0.5f);
            Assert.Greater(m.GetPixel(6, 5).r, 0.5f, "connected blob kept");
            Assert.AreEqual(0f, m.GetPixel(5, 8).r, 1e-3f, "floating speck kept");
        }

        [Test]
        public void Build_SkipsTheFrameCorners() {
            // A frame whose top and bottom bands cross the whole strip: those corner rows must not reach the mask.
            var m = Build(Source(32, 32, (x, y) => x < 2 || y < 4 || y >= 28), 0.25f);
            for (int y = 0; y < m.height; y++) Assert.AreEqual(0f, m.GetPixel(5, y).r, 1e-3f, $"row {y}");
        }

        [Test]
        public void Build_TinyBorder_ReadsAtLeastOneColumn() => Assert.AreEqual(1, Build(Source(32, 32, (x, y) => true), 0.001f).width);

        [Test]
        public void Build_AllWhite_IsEmpty() {
            foreach (var c in Build(Source(16, 16, (x, y) => false), 0.5f).GetPixels()) Assert.AreEqual(0f, c.r, 1e-3f);
        }

        [Test]
        public void Build_AllBlack_IsFull() {
            foreach (var c in Build(Source(16, 16, (x, y) => true), 0.5f).GetPixels()) Assert.Greater(c.r, 0.9f);
        }

        [Test]
        public void Build_RestoresTheActiveRenderTexture() {
            var mine = new RenderTexture(4, 4, 0);
            created.Add(mine);
            var previous = RenderTexture.active;
            RenderTexture.active = mine;
            try {
                Build(Source(8, 8, (x, y) => x < 2), 0.5f);
                Assert.AreEqual(mine, RenderTexture.active);
            } finally {
                RenderTexture.active = previous;
            }
        }

        [Test]
        public void Build_SingleChannelTexture_ReadsItsValue([Values(TextureFormat.R8, TextureFormat.Alpha8)] TextureFormat format) {
            var tex = new Texture2D(32, 32, format, false);
            var pixels = new Color[32 * 32];
            for (int y = 0; y < 32; y++)
                for (int x = 0; x < 32; x++) {
                    var v = x < 3 ? 0f : 1f;
                    pixels[y * 32 + x] = new Color(v, v, v, v);
                }
            tex.SetPixels(pixels);
            tex.Apply(false, true);
            created.Add(tex);
            var m = Build(tex, 0.5f);
            Assert.Greater(m.GetPixel(1, 8).r, 0.5f, "dark rim lost");
            Assert.AreEqual(0f, m.GetPixel(10, 8).r, 1e-3f, "white read as worn");
        }

        [Test]
        public void Build_MaxWidth_DownsamplesTheStrip() {
            var m = HotspotWearMask.Build(Source(64, 64, (x, y) => x < 8), 0.5f, 8);
            created.Add(m);
            Assert.AreEqual(8, m.width);
            Assert.AreEqual(8, m.height); // 64 − 2·16 skipped corner rows = 32, scaled by 8/32
            Assert.Greater(m.GetPixel(0, 6).r, 0.5f);
            Assert.AreEqual(0f, m.GetPixel(6, 6).r, 1e-3f);
        }
    }
}
