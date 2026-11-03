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
            recipe.top.texelsPerUnit = recipe.bottom.texelsPerUnit = 64f;
            recipe.wear.bevelWidth = 0.05f;
            recipe.wear.intensity = 0f;
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
            recipe.wear.chipDensity = 1f;
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
        public void SameSeed_IsDeterministic_DifferentSeed_Differs() {
            var recipe = CentredPatch();
            recipe.wear.intensity = 0.6f;
            recipe.seed = 3;
            var a = Read(recipe, HotspotMap.Wear).GetPixels32();
            var b = Read(recipe, HotspotMap.Wear).GetPixels32();
            recipe.seed = 4;
            var c = Read(recipe, HotspotMap.Wear).GetPixels32();
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
            recipe.wear.chipSize = 0.25f;
            var wear = Read(recipe, HotspotMap.Wear);

            float Column(int x0, int x1) {
                var sum = 0f;
                for (int y = 17; y < 47; y++) sum += Mathf.Abs(wear.GetPixel(x0, y).r - wear.GetPixel(x1, y).r);
                return sum;
            }
            var seam = Column(63, 0);
            var neighbours = 0f;
            for (int x = 0; x < 63; x++) neighbours += Column(x, x + 1);
            neighbours /= 63f;
            Assert.LessOrEqual(seam, neighbours * 3f + 0.5f);
        }
    }
}
