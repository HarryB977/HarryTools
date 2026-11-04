using System.Collections.Generic;
using System.IO;
using HarryTools.ProBuilderExtensions.Generator;
using HarryTools.ProBuilderExtensions.HotspotUV;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HarryTools.ProBuilderExtensions.Tests {
    public class HotspotRendererTests {
        private const int Size = 64;
        private const string TempFolder = "Assets/__HotspotRendererTests";
        private readonly List<Object> created = new();

        [TearDown]
        public void TearDown() {
            foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
            if (AssetDatabase.IsValidFolder(TempFolder)) AssetDatabase.DeleteAsset(TempFolder);
        }

        /// <summary>A solid-colour, non-readable layer texture, like an imported asset.</summary>
        private Texture2D Solid(Color color) {
            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            var pixels = new Color[64];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            tex.SetPixels(pixels);
            tex.Apply(false, true);
            created.Add(tex);
            return tex;
        }

        private HotspotRecipe Recipe(params HotspotPatch[] patches) {
            var layout = ScriptableObject.CreateInstance<HotspotLayout>();
            layout.worldWidth = 1f; // 64 px → 64 px/m
            layout.patches = new List<HotspotPatch>(patches);
            var recipe = ScriptableObject.CreateInstance<HotspotRecipe>();
            recipe.layout = layout;
            recipe.top.albedo = Solid(Color.green);
            recipe.bottom.albedo = Solid(Color.red);
            recipe.top.tileSize = recipe.bottom.tileSize = 0.125f; // 8 px texture at 64 px/m
            recipe.wear.bevelWidth = 0.05f;
            recipe.wear.intensity = 0f;
            recipe.wear.style = HotspotWearStyle.Bites;
            recipe.wear.edgeWidth = 0f;
            created.Add(layout);
            created.Add(recipe);
            return recipe;
        }

        private HotspotRecipe CentredPatch() => Recipe(HotspotPatch.Default(new Rect(0.125f, 0.125f, 0.75f, 0.75f)));

        private Texture2D Read(HotspotRecipe recipe, HotspotMap map, bool linear = true) {
            using var renderer = new HotspotRenderer(recipe, Size);
            var tex = renderer.ReadBack(map, linear);
            created.Add(tex);
            return tex;
        }

        /// <summary>Fraction of patch pixels (8..55) where the wear map is above 0.5.</summary>
        private float Coverage(HotspotRecipe recipe) {
            var wear = Read(recipe, HotspotMap.Wear);
            int worn = 0, total = 0;
            for (int y = 8; y < 56; y++)
                for (int x = 8; x < 56; x++, total++)
                    if (wear.GetPixel(x, y).r > 0.5f) worn++;
            return (float)worn / total;
        }

        [Test]
        public void Height_TopInCenter_GapOutside_MonotonicAcrossBevel() {
            var height = Read(CentredPatch(), HotspotMap.Height);
            var center = height.GetPixel(32, 32).r;
            Assert.Greater(center, height.GetPixel(2, 32).r + 0.1f, "gap should be lower than the top");
            Assert.Greater(center, height.GetPixel(8, 32).r + 0.05f, "the border should be lower than the top");
            for (int x = 8; x < 14; x++)
                Assert.GreaterOrEqual(height.GetPixel(x + 1, 32).r, height.GetPixel(x, 32).r - 1e-3f, $"bevel not monotonic at x={x}");
        }

        [Test]
        public void IntensityZero_NoBottomLayerInsidePatch_ButEdgeToneDiffers() {
            var color = Read(CentredPatch(), HotspotMap.BaseColor, linear: false);
            for (int y = 8; y < 56; y++)
                for (int x = 8; x < 56; x++) {
                    var c = color.GetPixel(x, y);
                    Assert.Greater(c.g, c.r, $"bottom layer visible at {x},{y}");
                }
            var center = color.GetPixel(32, 32);
            var edge = color.GetPixel(8, 32);
            Assert.Greater(Mathf.Abs(center.r - edge.r) + Mathf.Abs(center.g - edge.g) + Mathf.Abs(center.b - edge.b), 0.02f);
            Assert.Greater(color.GetPixel(2, 32).r, color.GetPixel(2, 32).g, "the gap shows the bottom layer");
        }

        [Test]
        public void IntensityOne_ShowsBottomNearEdges() {
            var recipe = CentredPatch();
            recipe.wear.intensity = 1f;
            var color = Read(recipe, HotspotMap.BaseColor, linear: false);
            var red = 0;
            for (int y = 8; y < 56; y++)
                for (int x = 8; x < 12; x++)
                    if (color.GetPixel(x, y).r > color.GetPixel(x, y).g) red++;
            Assert.Greater(red, 0);
        }

        [Test]
        public void Coverage_IsMonotonicInIntensity() {
            var recipe = CentredPatch();
            recipe.wear.intensity = 0f;
            var none = Coverage(recipe);
            recipe.wear.intensity = 0.5f;
            var half = Coverage(recipe);
            recipe.wear.intensity = 1f;
            var full = Coverage(recipe);
            Assert.AreEqual(0f, none);
            Assert.LessOrEqual(none, half);
            Assert.LessOrEqual(half, full);
            Assert.Greater(full, none);
        }

        [Test]
        public void SameSeed_IsDeterministic_DifferentSeed_Differs([Values(HotspotMap.Wear, HotspotMap.Style)] HotspotMap map) {
            var recipe = CentredPatch();
            recipe.wear.intensity = 0.6f;
            if (map == HotspotMap.Style) recipe.wear.style = HotspotWearStyle.Cracks;
            recipe.seed = 3;
            var a = Read(recipe, map).GetPixels32();
            var b = Read(recipe, map).GetPixels32();
            recipe.seed = 4;
            var c = Read(recipe, map).GetPixels32();
            var different = 0;
            for (int i = 0; i < a.Length; i++) {
                Assert.AreEqual(a[i], b[i], $"pixel {i}");
                if (!a[i].Equals(c[i])) different++;
            }
            Assert.Greater(different, 0);
        }

        [Test]
        public void NormalInput_IsUnpacked() {
            AssetDatabase.CreateFolder("Assets", "__HotspotRendererTests");
            var path = TempFolder + "/Flat_Normal.png";
            var flat = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            var pixels = new Color32[64];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(128, 128, 255, 255);
            flat.SetPixels32(pixels);
            File.WriteAllBytes(path, flat.EncodeToPNG());
            Object.DestroyImmediate(flat);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();

            var recipe = CentredPatch();
            recipe.top.normal = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var n = Read(recipe, HotspotMap.Normal).GetPixel(32, 32);
            Assert.AreEqual(0.5f, n.r, 0.03f);
            Assert.AreEqual(0.5f, n.g, 0.03f);
            Assert.Greater(n.b, 0.95f);
        }

        [Test]
        public void TilingStrip_HasNoBevelAtTheEnds() {
            var strip = HotspotPatch.Default(new Rect(0, 0.25f, 1, 0.5f));
            strip.allowTiling = true;
            var height = Read(Recipe(strip), HotspotMap.Height);
            var middle = height.GetPixel(32, 32).r;
            Assert.AreEqual(middle, height.GetPixel(0, 32).r, 0.01f);
            Assert.AreEqual(middle, height.GetPixel(63, 32).r, 0.01f);
        }

        [Test]
        public void TilingStrip_WearIsSeamlessAcrossTheWrap() {
            var strip = HotspotPatch.Default(new Rect(0, 0.25f, 1, 0.5f));
            strip.allowTiling = true;
            var recipe = Recipe(strip);
            recipe.wear.intensity = 1f;
            recipe.wear.wearSize = 0.25f;
            AssertSeamless(Read(recipe, HotspotMap.Wear));
        }

        /// <summary>The wrap column (63 → 0) of a horizontal strip changes no more than ordinary neighbouring columns.</summary>
        private static void AssertSeamless(Texture2D mask) {
            float Column(int x0, int x1) {
                var sum = 0f;
                for (int y = 17; y < 47; y++) sum += Mathf.Abs(mask.GetPixel(x0, y).r - mask.GetPixel(x1, y).r);
                return sum;
            }
            var seam = Column(63, 0);
            var neighbours = 0f;
            for (int x = 0; x < 63; x++) neighbours += Column(x, x + 1);
            neighbours /= 63f;
            Assert.LessOrEqual(seam, neighbours * 3f + 0.5f);
        }
        [Test]
        public void Rendering_RestoresTheActiveRenderTexture() {
            var mine = new RenderTexture(4, 4, 0);
            created.Add(mine);
            var previous = RenderTexture.active;
            RenderTexture.active = mine;
            try {
                using (var renderer = new HotspotRenderer(CentredPatch(), Size)) {
                    renderer.Render(HotspotMap.Normal);
                    Assert.AreEqual(mine, RenderTexture.active, "after Render");
                    var tex = renderer.ReadBack(HotspotMap.BaseColor, false);
                    created.Add(tex);
                    Assert.AreEqual(mine, RenderTexture.active, "after ReadBack");
                }
                Assert.AreEqual(mine, RenderTexture.active, "after Dispose");
            } finally {
                RenderTexture.active = previous;
            }
        }

        [Test]
        public void Repetitions_KeepTheLayerUndistorted(
            [Values(8f, 4f)] float atlasHeight, [Values(1.5f, 0.7f, 2.3f)] float tileWidth, [Values(1f, 0.5f)] float tileAspect) {
            var world = new Vector2(8f, atlasHeight);
            var tile = new Vector2(tileWidth, tileWidth * tileAspect);
            var reps = HotspotRenderer.ComputeRepetitions(world, tile);
            Assert.AreEqual(Mathf.Round(reps.x), reps.x);
            Assert.AreEqual(Mathf.Round(reps.y), reps.y);
            var actual = new Vector2(world.x / reps.x, world.y / reps.y);
            var anisotropy = Mathf.Abs(Mathf.Log((actual.x / actual.y) / (tile.x / tile.y)));
            Assert.Less(anisotropy, Mathf.Log(1.05f), $"tile {actual} vs {tile}");
            Assert.Less(Mathf.Abs(Mathf.Log(actual.x / tile.x)), Mathf.Log(1.35f), $"scale {actual.x} vs {tile.x}");
        }
        /// <summary>Worn pixels inside the centred patch that cannot reach the patch border through other worn pixels.</summary>
        private static int Isolated(Texture2D mask) {
            const int min = 8, max = 55;
            bool Worn(int x, int y) => mask.GetPixel(x, y).r > 0.5f;
            var reached = new bool[Size, Size];
            var queue = new Queue<Vector2Int>();
            for (int i = min; i <= max; i++)
                foreach (var p in new[] { new Vector2Int(min, i), new Vector2Int(max, i), new Vector2Int(i, min), new Vector2Int(i, max) })
                    if (Worn(p.x, p.y) && !reached[p.x, p.y]) { reached[p.x, p.y] = true; queue.Enqueue(p); }
            while (queue.Count > 0) {
                var p = queue.Dequeue();
                foreach (var d in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down }) {
                    var q = p + d;
                    if (q.x < min || q.x > max || q.y < min || q.y > max || reached[q.x, q.y] || !Worn(q.x, q.y)) continue;
                    reached[q.x, q.y] = true;
                    queue.Enqueue(q);
                }
            }
            var isolated = 0;
            for (int y = min; y <= max; y++)
                for (int x = min; x <= max; x++)
                    if (Worn(x, y) && !reached[x, y]) isolated++;
            return isolated;
        }

        [Test]
        public void Wear_IsAlwaysConnectedToTheEdge([Values(0.3f, 0.6f, 1f)] float intensity, [Values(0, 7)] int seed) {
            var recipe = CentredPatch();
            recipe.wear.intensity = intensity;
            recipe.wear.wearSize = 0.05f;
            recipe.seed = seed;
            var wear = Read(recipe, HotspotMap.Wear);
            Assert.Greater(Coverage(recipe), 0f);
            Assert.AreEqual(0, Isolated(wear), "wear must grow from the patch border");
        }

        [Test]
        public void Cavity_DarkensGapsEvenWithIdenticalLayers() {
            var recipe = CentredPatch();
            recipe.bottom.albedo = recipe.top.albedo = Solid(new Color(0.5f, 0.5f, 0.5f));
            recipe.wear.edgeTone = 0f;
            recipe.wear.cavity = 0.5f;
            var color = Read(recipe, HotspotMap.BaseColor, linear: false);
            Assert.Greater(color.GetPixel(32, 32).r - color.GetPixel(2, 32).r, 0.1f, "gap should be visibly darker");

            recipe.wear.cavity = 0f;
            var flat = Read(recipe, HotspotMap.BaseColor, linear: false);
            Assert.AreEqual(flat.GetPixel(32, 32).r, flat.GetPixel(2, 32).r, 0.01f);
        }
        /// <summary>A non-readable 8×8 texture where every texel has a different colour.</summary>
        private Texture2D Gradient() {
            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var pixels = new Color[64];
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 8; x++) pixels[y * 8 + x] = new Color(x / 7f, y / 7f, (x + y) % 3 / 2f);
            tex.SetPixels(pixels);
            tex.Apply(false, true);
            created.Add(tex);
            return tex;
        }

        private HotspotRecipe TwoPatches(Texture2D top) {
            var a = HotspotPatch.Default(new Rect(0.0625f, 0.0625f, 0.375f, 0.375f));
            var b = HotspotPatch.Default(new Rect(0.5625f, 0.5f, 0.375f, 0.375f));
            var recipe = Recipe(a, b);
            recipe.top.albedo = top;
            recipe.top.tileSize = 0.25f;
            recipe.wear.edgeTone = 0f;
            recipe.wear.cavity = 0f;
            return recipe;
        }

        [Test]
        public void VariationZero_SeedDoesNotChangeTheLayer() {
            var recipe = TwoPatches(Gradient());
            recipe.variation = 0f;
            recipe.seed = 1;
            var a = Read(recipe, HotspotMap.BaseColor);
            recipe.seed = 2;
            var b = Read(recipe, HotspotMap.BaseColor);
            Assert.AreEqual(a.GetPixel(15, 48), b.GetPixel(15, 48));

            recipe.variation = 1f;
            var c = Read(recipe, HotspotMap.BaseColor);
            recipe.seed = 1;
            var d = Read(recipe, HotspotMap.BaseColor);
            Assert.AreNotEqual(c.GetPixel(15, 48), d.GetPixel(15, 48), "with full variation the seed moves the layer");
        }

        [Test]
        public void TileSize_SetsTheRepeatInMetres() {
            var recipe = CentredPatch();
            recipe.top.albedo = Gradient();
            recipe.top.tileSize = 0.5f; // half the 1 m atlas → repeats every 32 px
            recipe.variation = 0f;
            recipe.wear.edgeTone = 0f;
            recipe.wear.cavity = 0f;
            var color = Read(recipe, HotspotMap.BaseColor);
            for (int y = 16; y < 48; y += 7) {
                var a = color.GetPixel(14, y);
                var b = color.GetPixel(46, y);
                Assert.AreEqual(a.r, b.r, 0.02f, $"row {y}");
                Assert.AreEqual(a.g, b.g, 0.02f, $"row {y}");
            }
        }

        [Test]
        public void AlignToPatches_StartsEveryPatchAtTheSamePointOfTheLayer() {
            var recipe = TwoPatches(Gradient());
            recipe.top.alignToPatches = true;
            recipe.variation = 1f;
            recipe.seed = 5;
            var color = Read(recipe, HotspotMap.BaseColor);
            // Patch A bottom-left: pixel (4, 36); patch B bottom-left: pixel (36, 8) (texture y up).
            for (int dy = 2; dy < 20; dy += 5)
                for (int dx = 2; dx < 20; dx += 5) {
                    var a = color.GetPixel(4 + dx, 36 + dy);
                    var b = color.GetPixel(36 + dx, 8 + dy);
                    Assert.AreEqual(a.r, b.r, 0.02f, $"offset {dx},{dy}");
                    Assert.AreEqual(a.g, b.g, 0.02f, $"offset {dx},{dy}");
                }
        }

        /// <summary>Metres from the pixel centre to the border of the centred patch (pixels 8..56 at 64 px/m).</summary>
        private static float BorderDistance(int x, int y) =>
            Mathf.Min(Mathf.Min(x + 0.5f - 8, 56 - x - 0.5f), Mathf.Min(y + 0.5f - 8, 56 - y - 0.5f)) / 64f;

        [Test]
        public void Rim_HasNoGaps([Values(HotspotWearStyle.Cracks, HotspotWearStyle.Bites, HotspotWearStyle.None)] HotspotWearStyle style,
            [Values(0f, 1f)] float intensity, [Values(0.01f, 0.25f)] float wearSize, [Values(0, 7)] int seed) {
            var recipe = CentredPatch();
            recipe.wear.style = style;
            recipe.wear.intensity = intensity;
            recipe.wear.wearSize = wearSize;
            recipe.wear.edgeWidth = 0.05f;
            recipe.seed = seed;
            var wear = Read(recipe, HotspotMap.Wear);
            var color = Read(recipe, HotspotMap.BaseColor, linear: false);
            for (int y = 8; y < 56; y++)
                for (int x = 8; x < 56; x++) {
                    if (BorderDistance(x, y) >= 0.05f - 1f / 64f) continue;
                    Assert.Greater(wear.GetPixel(x, y).r, 0.5f, $"rim gap at {x},{y}");
                    var c = color.GetPixel(x, y);
                    Assert.Greater(c.r, c.g, $"rim not showing the bottom layer at {x},{y}");
                }
        }

        [Test]
        public void Rim_ThinnerThanAPixel_StillDrawsOne() {
            var recipe = CentredPatch();
            recipe.wear.edgeWidth = 0.001f;
            recipe.wear.style = HotspotWearStyle.None;
            var wear = Read(recipe, HotspotMap.Wear);
            for (int i = 8; i < 56; i++) {
                Assert.Greater(wear.GetPixel(8, i).r, 0.5f, $"left {i}");
                Assert.Greater(wear.GetPixel(55, i).r, 0.5f, $"right {i}");
                Assert.Greater(wear.GetPixel(i, 8).r, 0.5f, $"bottom {i}");
                Assert.Greater(wear.GetPixel(i, 55).r, 0.5f, $"top {i}");
            }
        }

        [Test]
        public void Rim_InPatchNarrowerThanTwoRims_WearsItAll() {
            var recipe = Recipe(HotspotPatch.Default(new Rect(0.25f, 0.25f, 4f / 64f, 0.5f)));
            recipe.wear.edgeWidth = 0.05f;
            recipe.wear.style = HotspotWearStyle.None;
            var wear = Read(recipe, HotspotMap.Wear);
            for (int y = 17; y < 47; y++)
                for (int x = 16; x < 20; x++) {
                    var v = wear.GetPixel(x, y).r;
                    Assert.IsFalse(float.IsNaN(v), $"{x},{y}");
                    Assert.Greater(v, 0.5f, $"{x},{y}");
                }
        }

        [Test]
        public void EdgeWidthZero_NoRim_ButBevelRemains() {
            var recipe = CentredPatch();
            recipe.wear.style = HotspotWearStyle.None;
            recipe.wear.edgeWidth = 0f;
            Assert.AreEqual(0f, Coverage(recipe));
            var height = Read(recipe, HotspotMap.Height);
            Assert.Greater(height.GetPixel(32, 32).r, height.GetPixel(8, 32).r + 0.05f);
        }

        [Test]
        public void Rim_InnerOutlineIsIrregular() {
            var recipe = CentredPatch();
            recipe.wear.edgeWidth = 0.08f;
            recipe.wear.style = HotspotWearStyle.None;
            var wear = Read(recipe, HotspotMap.Wear);
            var depths = new HashSet<int>();
            for (int y = 12; y < 52; y++) {
                int d = 0;
                while (d < 20 && wear.GetPixel(8 + d, y).r > 0.5f) d++;
                depths.Add(d);
            }
            Assert.Greater(depths.Count, 1, "rim inner outline is a straight line");
        }

        private HotspotRecipe Cracks(float intensity, int seed) {
            var recipe = CentredPatch();
            recipe.wear.style = HotspotWearStyle.Cracks;
            recipe.wear.edgeWidth = 0f;
            recipe.wear.wearSize = 0.1f;
            recipe.wear.bevelWidth = 0.02f;
            recipe.wear.intensity = intensity;
            recipe.seed = seed;
            return recipe;
        }

        [Test]
        public void Cracks_AreConnectedToTheEdge([Values(0.3f, 0.6f, 1f)] float intensity, [Values(0, 7)] int seed) {
            var mask = Read(Cracks(intensity, seed), HotspotMap.Style);
            var any = false;
            for (int y = 8; y < 56; y++)
                for (int x = 8; x < 56; x++) any |= mask.GetPixel(x, y).r > 0.5f;
            Assert.IsTrue(any, "no cracks at all");
            Assert.AreEqual(0, Isolated(mask));
        }

        [Test]
        public void Cracks_ReachBeyondTheBevel() {
            var mask = Read(Cracks(1f, 0), HotspotMap.Style);
            var deep = 0;
            for (int y = 8; y < 56; y++)
                for (int x = 8; x < 56; x++)
                    if (BorderDistance(x, y) > 0.03f && mask.GetPixel(x, y).r > 0.5f) deep++;
            Assert.Greater(deep, 0);
        }

        [Test]
        public void Cracks_DoNotShowTheBottomLayer() => Assert.AreEqual(0f, Coverage(Cracks(1f, 3)));

        [Test]
        public void Cracks_LowerTheHeight() {
            var recipe = Cracks(1f, 0);
            var mask = Read(recipe, HotspotMap.Style);
            var height = Read(recipe, HotspotMap.Height);
            var plateau = height.GetPixel(32, 32).r;
            var found = false;
            for (int y = 8; y < 56; y++)
                for (int x = 8; x < 56; x++)
                    if (BorderDistance(x, y) > 0.03f && mask.GetPixel(x, y).r > 0.9f) {
                        found = true;
                        Assert.Less(height.GetPixel(x, y).r, plateau - 0.05f, $"{x},{y}");
                    }
            Assert.IsTrue(found);
        }

        [Test]
        public void Cracks_IntensityZero_NoCracks() {
            var mask = Read(Cracks(0f, 0), HotspotMap.Style);
            foreach (var c in mask.GetPixels()) Assert.AreEqual(0f, c.r, 1e-3f);
        }

        [Test]
        public void Cracks_StayInsideTheirPatch() {
            var recipe = TwoPatches(Solid(Color.green));
            recipe.wear.style = HotspotWearStyle.Cracks;
            recipe.wear.edgeWidth = 0f;
            recipe.wear.intensity = 1f;
            recipe.wear.wearSize = 0.1f;
            var mask = Read(recipe, HotspotMap.Style);
            // Patch A: px x 4..28, y 36..60 (texture y up); patch B: x 36..60, y 8..32.
            bool InA(int x, int y) => x >= 4 && x < 28 && y >= 36 && y < 60;
            bool InB(int x, int y) => x >= 36 && x < 60 && y >= 8 && y < 32;
            bool a = false, b = false;
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++) {
                    var v = mask.GetPixel(x, y).r;
                    if (InA(x, y)) a |= v > 0.5f;
                    else if (InB(x, y)) b |= v > 0.5f;
                    else Assert.AreEqual(0f, v, 1e-3f, $"{x},{y}");
                }
            Assert.IsTrue(a && b, "every patch should have cracks");
        }

        [Test]
        public void Cracks_LongerThanThePatch_StillAppearAndStayConnected([Values(0, 7)] int seed) {
            var recipe = Cracks(1f, seed);
            recipe.wear.wearSize = 0.5f; // cracks 0.5–1 m long on a 0.75 m patch
            var mask = Read(recipe, HotspotMap.Style);
            var any = false;
            for (int y = 8; y < 56; y++)
                for (int x = 8; x < 56; x++) any |= mask.GetPixel(x, y).r > 0.5f;
            Assert.IsTrue(any, "raising Intensity must not remove the cracks");
            Assert.AreEqual(0, Isolated(mask));
        }

        [Test]
        public void Cracks_SeamlessAcrossTheWrap() {
            var strip = HotspotPatch.Default(new Rect(0, 0.25f, 1, 0.5f));
            strip.allowTiling = true;
            var recipe = Recipe(strip);
            recipe.wear.style = HotspotWearStyle.Cracks;
            recipe.wear.intensity = 1f;
            recipe.wear.wearSize = 0.1f;
            AssertSeamless(Read(recipe, HotspotMap.Style));
        }

        /// <summary>A non-readable grunge frame, black where <paramref name="black"/> is true and white elsewhere.</summary>
        private Texture2D Frame(int w, int h, System.Func<int, int, bool> black) {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) pixels[y * w + x] = black(x, y) ? Color.black : Color.white;
            tex.SetPixels(pixels);
            tex.Apply(false, true);
            created.Add(tex);
            return tex;
        }

        private HotspotRecipe TextureRecipe(params HotspotPatch[] patches) {
            var recipe = Recipe(patches);
            recipe.wear.style = HotspotWearStyle.None;
            recipe.wear.edgeWidth = 0f;
            recipe.wear.intensity = 1f;
            recipe.wear.textureBorder = 0.5f;
            recipe.wear.textureDepth = 0.125f; // 8 px
            return recipe;
        }

        private Texture2D DepthFrame() => Frame(32, 32, (x, y) => x < 8); // left half of the 16-px strip worn → 4 px deep

        [Test]
        public void WearTexture_DepthIsInMetres_WhateverThePatchSize() {
            var frame = DepthFrame();
            foreach (var rect in new[] { new Rect(0.125f, 0.125f, 0.75f, 0.75f), new Rect(0.125f, 0.375f, 0.75f, 0.25f) }) {
                var recipe = TextureRecipe(HotspotPatch.Default(rect));
                recipe.wear.wearTexture = frame;
                var wear = Read(recipe, HotspotMap.Wear);
                int d = 0;
                while (d < 30 && wear.GetPixel(8 + d, 32).r > 0.5f) d++;
                Assert.AreEqual(4, d, 1, rect.ToString());
            }
        }

        [Test]
        public void WearTexture_ShowsTheBottomLayer() {
            var recipe = TextureRecipe(HotspotPatch.Default(new Rect(0.125f, 0.125f, 0.75f, 0.75f)));
            recipe.wear.wearTexture = DepthFrame();
            var c = Read(recipe, HotspotMap.BaseColor, linear: false).GetPixel(9, 32);
            Assert.Greater(c.r, c.g);
        }

        [Test]
        public void WearTexture_IsConnectedToTheEdge() {
            var recipe = TextureRecipe(HotspotPatch.Default(new Rect(0.125f, 0.125f, 0.75f, 0.75f)));
            recipe.wear.wearTexture = Frame(32, 32, (x, y) => x < 2 + (y * 7 % 5) || (x == 9 && y % 6 == 0));
            Assert.AreEqual(0, Isolated(Read(recipe, HotspotMap.Wear)));
        }

        [Test]
        public void WearTexture_LowerIntensity_WearsLess() {
            var recipe = TextureRecipe(HotspotPatch.Default(new Rect(0.125f, 0.125f, 0.75f, 0.75f)));
            recipe.wear.wearTexture = DepthFrame();
            var full = Coverage(recipe);
            recipe.wear.intensity = 0.5f;
            var half = Coverage(recipe);
            Assert.Greater(half, 0f);
            Assert.Less(half, full);
        }

        [Test]
        public void WearTexture_SeamlessAcrossTheWrap() {
            var strip = HotspotPatch.Default(new Rect(0, 0.25f, 1, 0.5f));
            strip.allowTiling = true;
            var recipe = TextureRecipe(strip);
            recipe.wear.wearTexture = Frame(32, 32, (x, y) => x < 1 + y % 8);
            AssertSeamless(Read(recipe, HotspotMap.Wear));
        }

        [Test]
        public void WearTexture_CombinesWithTheStyle() {
            var recipe = TextureRecipe(HotspotPatch.Default(new Rect(0.125f, 0.125f, 0.75f, 0.75f)));
            recipe.wear.style = HotspotWearStyle.Bites;
            recipe.wear.wearSize = 0.25f;
            var bites = Coverage(recipe);
            recipe.wear.wearTexture = Frame(32, 32, (x, y) => x < 1 + y % 8);
            var both = Coverage(recipe);
            recipe.wear.style = HotspotWearStyle.None;
            var texture = Coverage(recipe);
            Assert.Greater(bites, 0f);
            Assert.Greater(texture, 0f);
            Assert.GreaterOrEqual(both, Mathf.Max(bites, texture));
        }

        [Test]
        public void WearTexture_FineDetailFarBelowAPixel_LeavesNoIslands() {
            var recipe = TextureRecipe(HotspotPatch.Default(new Rect(0.125f, 0.125f, 0.75f, 0.75f)));
            // 256-texel strip squeezed into an 8 px band: one-texel filaments connected to the rim.
            recipe.wear.wearTexture = Frame(512, 512, (x, y) => x < 4 || (x < 200 && (y + x / 3) % 16 == 0));
            Assert.AreEqual(0, Isolated(Read(recipe, HotspotMap.Wear)));
        }

        [Test]
        public void StyleMap_ShowsBites() {
            var recipe = CentredPatch();
            recipe.wear.style = HotspotWearStyle.Bites;
            recipe.wear.intensity = 1f;
            recipe.wear.wearSize = 0.25f;
            var style = Read(recipe, HotspotMap.Style);
            var wear = Read(recipe, HotspotMap.Wear);
            var any = false;
            for (int y = 8; y < 56; y++)
                for (int x = 8; x < 56; x++) {
                    var s = style.GetPixel(x, y).r;
                    any |= s > 0.5f;
                    Assert.AreEqual(wear.GetPixel(x, y).r, s, 0.02f, $"{x},{y}"); // no rim or texture here: wear = bites
                }
            Assert.IsTrue(any);
        }

        [Test]
        public void Bites_NarrowerThanAPixel_StillShowPartially() {
            var recipe = CentredPatch();
            recipe.wear.intensity = 1f;
            recipe.wear.wearSize = 0.02f; // at most 0.025 m · n = 1.6 px, mostly well under a pixel
            recipe.wear.intensity = 0.2f; // at most 0.32 px
            var wear = Read(recipe, HotspotMap.Wear);
            var max = 0f;
            for (int y = 8; y < 56; y++) max = Mathf.Max(max, wear.GetPixel(8, y).r);
            Assert.Greater(max, 0.05f, "sub-pixel bites are dropped");
        }
    }
}
