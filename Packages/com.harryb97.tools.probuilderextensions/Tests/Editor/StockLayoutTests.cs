using System.Collections.Generic;
using HarryTools.ProBuilderExtensions.HotspotUV;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HarryTools.ProBuilderExtensions.Tests {
    /// <summary>
    /// Definitions and constraints of the stock interior layouts. All are 8×8 m (512 px/m at 4096 px).
    /// Run <see cref="RebuildStockLayouts"/> (explicit) after changing a definition.
    /// </summary>
    public class StockLayoutTests {
        private const string Folder = "Packages/com.harryb97.tools.probuilderextensions/Layouts";
        private const float World = 8f;
        private const float Gap = 0.03f; // ≈ 15 px
        private static readonly string[] Names = { "Walls", "Floors", "Trims", "Frames", "Generic" };

        // ---------- Definitions ----------

        private sealed class Builder {
            public readonly List<HotspotPatch> patches = new();
            public float y = Gap;
            public HotspotPatch template = HotspotPatch.Default(default);

            /// <summary>A row of patches of the given widths (metres), left to right. Returns the row's height.</summary>
            public float Row(float height, params float[] widths) => RowAt(Gap, height, widths);

            public float RowAt(float x, float height, params float[] widths) {
                foreach (var w in widths) {
                    Add(x, y, w, height);
                    x += w + Gap;
                }
                Assert.LessOrEqual(x, World + 1e-4f, $"row at y={y} overflows: ends at {x}");
                return height;
            }

            public void Next(float height) => y += height + Gap;

            /// <summary>A full-width tiling strip.</summary>
            public void Strip(float height) {
                var p = template;
                p.rect = new Rect(0, y / World, 1, height / World);
                p.allowTiling = true;
                patches.Add(p);
                Next(height);
            }

            public void Add(float x, float yTop, float w, float h) {
                var p = template;
                p.rect = new Rect(x / World, yTop / World, w / World, h / World);
                patches.Add(p);
            }

            public HotspotLayout Build() {
                Assert.LessOrEqual(y, World + 1e-4f, $"layout overflows vertically: {y}");
                var layout = ScriptableObject.CreateInstance<HotspotLayout>();
                layout.worldWidth = World;
                layout.recommendedResolution = 4096;
                layout.patches = patches;
                return layout;
            }
        }

        // Each width covers [w·0.8, w·1.25]; consecutive widths overlap so every size in range finds a patch.
        private static readonly float[] WallWidths = { 0.37f, 0.57f, 0.88f, 1.36f, 2.1f, 3.25f };
        private const float WallHeight = 2.6f;
        private const float HalfWallHeight = 1.2f;

        private static HotspotLayout Walls() {
            var b = new Builder();
            b.template.allowRotation = false;
            b.template.allowMirrorX = true;

            b.Next(b.Row(WallHeight, 3.25f, 2.1f, 1.36f, 0.88f));

            // Narrow full walls on the left, two rows of half walls (waist height) beside them.
            b.Row(WallHeight, 0.57f, 0.37f);
            var x = Gap + 0.57f + Gap + 0.37f + Gap;
            var top = b.y;
            b.RowAt(x, HalfWallHeight, 2.1f, 1.36f, 0.88f, 0.57f, 0.37f);
            b.y = top + HalfWallHeight + Gap;
            b.RowAt(x, HalfWallHeight, 2.1f, 1.36f, 0.88f, 0.57f, 0.37f);
            b.y = top;
            b.RowAt(x + 2.1f + 1.36f + 0.88f + 0.57f + 0.37f + Gap * 5, WallHeight, 0.88f, 0.57f); // extra variants
            b.Next(WallHeight);

            b.Strip(WallHeight); // walls longer than ~4 m
            return b.Build();
        }

        private static HotspotLayout Floors() {
            var b = new Builder();
            b.template.allowMirrorX = true;
            b.template.allowMirrorY = true;

            // Planks: full-width strips that repeat along the plank.
            foreach (var w in new[] { 0.12f, 0.15f, 0.2f, 0.12f, 0.15f, 0.2f, 0.25f, 0.18f, 0.12f, 0.15f }) b.Strip(w);

            // Tiles and slabs.
            b.Next(b.Row(1.6f, 1.6f, 1.6f, 1.05f, 1.05f, 0.68f, 0.68f));
            b.Next(b.Row(1.05f, 1.05f, 1.05f, 0.68f, 0.68f, 0.44f, 0.44f, 0.44f, 0.3f, 0.3f, 0.3f));
            b.Row(2.45f, 2.45f, 2.45f, 1.6f);
            b.Next(2.45f);
            return b.Build();
        }

        // Full-width strips; each height covers [h·0.8, h·1.25].
        private static readonly float[] TrimHeights = { 0.06f, 0.09f, 0.14f, 0.21f, 0.3f };

        private static HotspotLayout Trims() {
            var b = new Builder();
            b.template.allowMirrorX = true;

            foreach (var h in TrimHeights) b.Strip(h);
            foreach (var h in TrimHeights) b.Strip(h); // second variant of each size
            foreach (var h in new[] { 0.4f, 0.6f, 0.9f, 1.2f }) b.Strip(h); // dados, wainscoting

            // End blocks, plinth blocks and rosettes.
            b.Next(b.Row(0.3f, 0.3f, 0.3f, 0.2f, 0.2f, 0.2f, 0.14f, 0.14f, 0.14f, 0.1f, 0.1f, 0.1f, 0.3f, 0.3f, 0.2f, 0.2f, 0.14f, 0.14f, 0.1f, 0.1f, 0.3f, 0.3f));
            b.Row(0.6f, 0.15f, 0.15f, 0.2f, 0.2f, 0.3f, 0.3f, 0.15f, 0.15f, 0.2f, 0.2f, 0.3f, 0.3f, 0.6f, 0.6f, 0.6f, 0.6f, 0.6f, 0.6f);
            b.Next(0.6f);
            return b.Build();
        }

        private static HotspotLayout Frames() {
            var b = new Builder();
            b.template.allowRotation = false;
            b.template.allowMirrorX = true;

            // Door jambs (vertical) beside window jambs.
            var top = b.y;
            b.Row(2.2f, 0.07f, 0.1f, 0.14f, 0.07f, 0.1f, 0.14f, 0.2f, 0.2f);
            var x = Gap + (0.07f + 0.1f + 0.14f) * 2 + 0.4f + Gap * 8;
            b.RowAt(x, 2.1f, 0.07f, 0.1f, 0.14f, 0.2f);
            x += 0.07f + 0.1f + 0.14f + 0.2f + Gap * 4;
            b.RowAt(x, 1.2f, 0.07f, 0.1f, 0.14f, 0.07f, 0.1f, 0.14f);
            x += (0.07f + 0.1f + 0.14f) * 2 + Gap * 6;
            b.RowAt(x, 1.6f, 0.07f, 0.1f, 0.14f, 0.07f, 0.1f, 0.14f);
            x += (0.07f + 0.1f + 0.14f) * 2 + Gap * 6;
            b.RowAt(x, 0.9f, 0.07f, 0.1f, 0.14f, 0.07f, 0.1f, 0.14f);
            b.y = top;
            b.Next(2.2f);

            // Lintels, heads and sills (horizontal).
            foreach (var h in new[] { 0.08f, 0.1f, 0.12f, 0.15f, 0.2f, 0.08f, 0.1f, 0.12f, 0.15f, 0.2f })
                b.Next(b.Row(h, 1.2f, 1.0f, 0.8f, 0.6f, 1.2f, 1.0f, 0.8f, 0.6f));

            // Window panes and door panels.
            b.Next(b.Row(1.2f, 0.6f, 0.6f, 0.9f, 0.9f, 1.2f, 0.45f, 0.45f, 0.45f, 0.3f, 0.3f, 0.3f, 0.3f));
            b.Next(b.Row(1.5f, 0.8f, 0.8f, 0.9f, 0.6f, 0.6f, 1.2f, 1.2f, 0.45f, 0.45f));
            b.Next(b.Row(1.2f, 0.9f, 0.9f, 0.6f, 0.6f, 0.45f, 0.45f, 0.3f, 0.3f, 1.2f, 1.2f));
            return b.Build();
        }

        // Squares; each side covers [s·0.8, s·1.25].
        private static readonly float[] GenericSides = { 0.12f, 0.185f, 0.285f, 0.44f, 0.68f, 1.05f, 1.62f };

        private static HotspotLayout Generic() {
            var b = new Builder();
            b.template.allowMirrorX = true;
            b.template.allowMirrorY = true;

            b.Next(b.Row(1.62f, 1.62f, 1.62f, 1.05f, 1.05f, 0.68f, 0.68f, 0.44f));
            b.Next(b.Row(1.05f, 2.1f, 2.1f, 1.05f, 1.05f, 0.68f, 0.44f, 0.285f));
            b.Next(b.Row(0.68f, 1.36f, 1.36f, 0.68f, 0.68f, 0.68f, 0.44f, 0.44f, 0.44f, 0.285f, 0.285f));
            b.Next(b.Row(0.44f, 0.88f, 0.88f, 0.88f, 0.44f, 0.44f, 0.44f, 0.44f, 0.285f, 0.285f, 0.285f, 0.185f, 0.185f, 0.185f, 0.12f, 0.12f, 0.12f, 0.12f));
            b.Next(b.Row(0.285f, 0.57f, 0.57f, 0.57f, 0.57f, 0.285f, 0.285f, 0.285f, 0.285f, 0.185f, 0.185f, 0.185f, 0.185f, 0.12f, 0.12f, 0.12f, 0.12f, 0.12f, 0.12f, 0.12f, 0.37f, 0.37f));
            b.Next(b.Row(0.185f, 0.37f, 0.37f, 0.37f, 0.37f, 0.37f, 0.185f, 0.185f, 0.185f, 0.185f, 0.185f, 0.185f, 0.12f, 0.12f, 0.12f, 0.12f, 0.12f, 0.12f, 0.24f, 0.24f, 0.24f, 0.24f, 0.24f, 0.24f));
            b.Next(b.Row(0.12f, 0.24f, 0.24f, 0.24f, 0.24f, 0.24f, 0.24f, 0.12f, 0.12f, 0.12f, 0.12f, 0.12f, 0.12f, 0.12f, 0.12f, 0.12f, 0.12f, 0.12f, 0.12f, 0.12f, 0.12f, 0.12f, 0.12f, 0.12f, 0.12f, 0.12f, 0.12f, 0.12f, 0.12f));
            b.Row(2.1f, 4.2f, 3.25f);
            b.Next(2.1f);
            return b.Build();
        }

        private static HotspotLayout Define(string name) => name switch {
            "Walls" => Walls(),
            "Floors" => Floors(),
            "Trims" => Trims(),
            "Frames" => Frames(),
            "Generic" => Generic(),
            _ => throw new System.ArgumentException(name),
        };

        [Test, Explicit("Writes the stock layout assets into the package.")]
        public void RebuildStockLayouts() {
            foreach (var name in Names) {
                var path = $"{Folder}/{name}.asset";
                var layout = Define(name);
                layout.name = name;
                var existing = AssetDatabase.LoadAssetAtPath<HotspotLayout>(path);
                if (existing != null) {
                    EditorUtility.CopySerialized(layout, existing);
                    existing.name = name;
                    EditorUtility.SetDirty(existing);
                    Object.DestroyImmediate(layout);
                } else {
                    AssetDatabase.CreateAsset(layout, path);
                }
            }
            AssetDatabase.SaveAssets();
        }

        // ---------- Constraints ----------

        private readonly List<Object> created = new();

        [TearDown]
        public void TearDown() {
            foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
        }

        private static HotspotLayout Load(string name) {
            var layout = AssetDatabase.LoadAssetAtPath<HotspotLayout>($"{Folder}/{name}.asset");
            Assert.IsNotNull(layout, $"Stock layout '{name}' is missing; run RebuildStockLayouts.");
            return layout;
        }

        private HotspotTexture Hotspot(string name) {
            var hotspot = ScriptableObject.CreateInstance<HotspotTexture>();
            hotspot.layout = Load(name);
            created.Add(hotspot);
            return hotspot;
        }

        private static HotspotMatch Match(HotspotTexture hotspot, float w, float h) =>
            hotspot.FindBestPatch(new Vector2(w, h), new HotspotMatchSettings {
                allowRotation = true, allowMirror = true, scaleTolerance = 0.25f, random = new System.Random(0),
            });

        private static Vector2 Metres(HotspotLayout layout, int i) => layout.patches[i].rect.size * layout.worldWidth;

        [Test]
        public void Assets_MatchDefinitions() {
            foreach (var name in Names) {
                var asset = Load(name);
                var defined = Define(name);
                created.Add(defined);
                Assert.AreEqual(defined.patches.Count, asset.patches.Count, $"{name}: run RebuildStockLayouts");
                for (int i = 0; i < defined.patches.Count; i++)
                    Assert.AreEqual(defined.patches[i], asset.patches[i], $"{name} patch {i}: run RebuildStockLayouts");
            }
        }

        [Test]
        public void All_ValidAndNonOverlapping() {
            foreach (var name in Names) {
                var layout = Load(name);
                var errors = new List<string>();
                Assert.IsTrue(layout.Validate(errors), $"{name}: {string.Join("; ", errors)}");
                Assert.AreEqual(8f, layout.worldWidth);
                Assert.AreEqual(4096, layout.recommendedResolution);
                Assert.Greater(layout.patches.Count, 0);
                for (int i = 0; i < layout.patches.Count; i++)
                    for (int j = i + 1; j < layout.patches.Count; j++)
                        Assert.IsFalse(layout.patches[i].rect.Overlaps(layout.patches[j].rect), $"{name}: patches {i} and {j} overlap");
            }
        }

        [Test]
        public void Walls_StayUpright_AndMirrorX() {
            foreach (var p in Load("Walls").patches) {
                Assert.IsFalse(p.allowRotation);
                Assert.IsTrue(p.allowMirrorX);
            }
        }

        [Test]
        public void Walls_EveryWidthFitsWithinTolerance([Values(2.7f, 1.2f)] float height) {
            var hotspot = Hotspot("Walls");
            var max = height > 2f ? 4.0f : 2.0f;
            for (var w = 0.3f; w <= max + 1e-4f; w += 0.05f) {
                var m = Match(hotspot, w, height);
                Assert.IsTrue(m.IsValid && !m.outOfTolerance, $"wall {w:0.00}×{height} m has no patch in tolerance");
                Assert.IsFalse(m.rotated);
            }
        }

        [Test]
        public void Walls_LongWallsUseTheTilingStrip([Values(6f, 12f)] float width) {
            var m = Match(Hotspot("Walls"), width, 2.7f);
            Assert.AreEqual(TilingAxis.Horizontal, m.tiling);
            Assert.IsFalse(m.outOfTolerance);
        }

        [Test]
        public void Trims_LongMouldingsTileAtEveryHeight() {
            var layout = Load("Trims");
            var strips = 0;
            for (int i = 0; i < layout.patches.Count; i++) if (layout.GetTilingAxis(i) == TilingAxis.Horizontal) strips++;
            Assert.GreaterOrEqual(strips, 4);

            var hotspot = Hotspot("Trims");
            for (var h = 0.05f; h <= 0.3f + 1e-4f; h += 0.01f) {
                var m = Match(hotspot, 5f, h);
                Assert.AreNotEqual(TilingAxis.None, m.tiling, $"moulding {h:0.00} m does not tile");
                Assert.IsFalse(m.outOfTolerance, $"moulding {h:0.00} m out of tolerance");
            }
        }

        [Test]
        public void Floors_HavePlanksAndTiles() {
            var layout = Load("Floors");
            bool planks = false, tiles = false;
            for (int i = 0; i < layout.patches.Count; i++) {
                if (layout.GetTilingAxis(i) != TilingAxis.None) planks = true;
                var s = Metres(layout, i);
                if (layout.GetTilingAxis(i) == TilingAxis.None && s.x / s.y >= 0.8f && s.x / s.y <= 1.25f) tiles = true;
            }
            Assert.IsTrue(planks, "no plank strips");
            Assert.IsTrue(tiles, "no square-ish tiles");
        }

        [Test]
        public void Frames_HaveJambsAndLintels() {
            var layout = Load("Frames");
            bool jamb = false, lintel = false;
            for (int i = 0; i < layout.patches.Count; i++) {
                var s = Metres(layout, i);
                if (s.y >= 2.1f - 1e-4f && s.x < s.y) jamb = true;
                if (s.x <= 1.2f + 1e-4f && s.x > s.y) lintel = true;
            }
            Assert.IsTrue(jamb, "no door jambs ≥ 2.1 m");
            Assert.IsTrue(lintel, "no lintels ≤ 1.2 m");
        }

        [Test]
        public void Generic_EverySquareFitsWithinTolerance() {
            var hotspot = Hotspot("Generic");
            for (var s = 0.1f; s <= 2.0f + 1e-4f; s += 0.05f) {
                var m = Match(hotspot, s, s);
                Assert.IsTrue(m.IsValid && !m.outOfTolerance, $"square {s:0.00} m has no patch in tolerance");
            }
        }
    }
}
