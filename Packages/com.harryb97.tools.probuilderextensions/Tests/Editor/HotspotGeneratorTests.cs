using System.Collections.Generic;
using HarryTools.ProBuilderExtensions.Generator;
using HarryTools.ProBuilderExtensions.HotspotUV;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HarryTools.ProBuilderExtensions.Tests {
    public class HotspotGeneratorTests {
        private const string Folder = "Assets/__HotspotGeneratorTests";
        private readonly List<Object> created = new();
        private HotspotLayout layout;

        [SetUp]
        public void SetUp() {
            if (AssetDatabase.IsValidFolder(Folder)) AssetDatabase.DeleteAsset(Folder);
            AssetDatabase.CreateFolder("Assets", "__HotspotGeneratorTests");
            layout = ScriptableObject.CreateInstance<HotspotLayout>();
            layout.patches = new List<HotspotPatch> {
                HotspotPatch.Default(new Rect(0.05f, 0.05f, 0.4f, 0.4f)),
                HotspotPatch.Default(new Rect(0.5f, 0.05f, 0.45f, 0.9f)),
            };
            AssetDatabase.CreateAsset(layout, Folder + "/Layout.asset");
        }

        [TearDown]
        public void TearDown() {
            foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
            AssetDatabase.DeleteAsset(Folder);
        }

        private HotspotRecipe Recipe(string name) {
            var albedo = new Texture2D(4, 4);
            created.Add(albedo);
            var recipe = ScriptableObject.CreateInstance<HotspotRecipe>();
            recipe.layout = layout;
            recipe.resolution = 128;
            recipe.top.albedo = albedo;
            recipe.bottom.albedo = albedo;
            AssetDatabase.CreateAsset(recipe, $"{Folder}/{name}.asset");
            return recipe;
        }

        private static TextureImporter Importer(Object asset) => (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(asset));

        private static string Guid(Object asset) => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));

        private static string[] Guids(HotspotRecipe r) => new[] {
            Guid(r.baseColorMap), Guid(r.normalMap), Guid(r.maskMap), Guid(r.occlusionMap), Guid(r.material), Guid(r.hotspotTexture),
        };

        [Test]
        public void GetOutputFolder_IsSubfolderNamedAfterRecipe() {
            Assert.AreEqual(Folder + "/Plaster", HotspotGenerator.GetOutputFolder(Recipe("Plaster")));
        }

        [Test]
        public void Generate_CreatesAllOutputs_WithImportSettings() {
            var recipe = Recipe("Plaster");
            HotspotGenerator.Generate(recipe);

            Assert.AreEqual(Folder + "/Plaster/Plaster_BaseColor.png", AssetDatabase.GetAssetPath(recipe.baseColorMap));
            Assert.IsTrue(Importer(recipe.baseColorMap).sRGBTexture);
            Assert.AreEqual(TextureImporterType.NormalMap, Importer(recipe.normalMap).textureType);
            Assert.IsFalse(Importer(recipe.maskMap).sRGBTexture);
            Assert.IsFalse(Importer(recipe.occlusionMap).sRGBTexture);
            foreach (var map in new[] { recipe.baseColorMap, recipe.normalMap, recipe.maskMap, recipe.occlusionMap }) {
                Assert.AreEqual(TextureWrapMode.Repeat, Importer(map).wrapMode, map.name);
                Assert.IsTrue(Importer(map).mipmapEnabled, map.name);
                Assert.AreEqual(128, map.width, map.name);
            }

            var mat = recipe.material;
            Assert.AreEqual("Universal Render Pipeline/Lit", mat.shader.name);
            Assert.AreEqual(recipe.baseColorMap, mat.GetTexture("_BaseMap"));
            Assert.AreEqual(recipe.normalMap, mat.GetTexture("_BumpMap"));
            Assert.AreEqual(recipe.maskMap, mat.GetTexture("_MetallicGlossMap"));
            Assert.AreEqual(recipe.occlusionMap, mat.GetTexture("_OcclusionMap"));
            Assert.IsTrue(mat.IsKeywordEnabled("_NORMALMAP"));
            Assert.IsTrue(mat.IsKeywordEnabled("_METALLICSPECGLOSSMAP"));
            Assert.IsTrue(mat.IsKeywordEnabled("_OCCLUSIONMAP"));

            var hotspot = recipe.hotspotTexture;
            Assert.AreEqual(layout, hotspot.layout);
            Assert.AreEqual(recipe.baseColorMap, hotspot.target);
            Assert.AreEqual(16f, hotspot.EffectiveTexelsPerUnit, 1e-3f); // 128 px over 8 m
        }

        [Test]
        public void WorldWidthOverride_ReachesHotspotTexture() {
            var recipe = Recipe("Plaster");
            recipe.worldWidthOverride = 4f;
            HotspotGenerator.Generate(recipe);
            Assert.AreEqual(32f, recipe.hotspotTexture.EffectiveTexelsPerUnit, 1e-3f);
        }

        [Test]
        public void Regenerate_KeepsGuids() {
            var recipe = Recipe("Plaster");
            HotspotGenerator.Generate(recipe);
            var before = Guids(recipe);
            recipe.wear.intensity = 0.9f;
            HotspotGenerator.Generate(recipe);
            CollectionAssert.AreEqual(before, Guids(recipe));
        }

        [Test]
        public void Regenerate_AfterOutputDeleted_RecreatesOnlyThat() {
            var recipe = Recipe("Plaster");
            HotspotGenerator.Generate(recipe);
            var before = Guids(recipe);
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(recipe.normalMap));

            HotspotGenerator.Generate(recipe);
            var after = Guids(recipe);
            Assert.IsNotNull(recipe.normalMap);
            Assert.AreEqual(Folder + "/Plaster/Plaster_Normal.png", AssetDatabase.GetAssetPath(recipe.normalMap));
            Assert.AreEqual(TextureImporterType.NormalMap, Importer(recipe.normalMap).textureType);
            // Unity may reuse the GUID of an asset recreated at the same path, so the deleted output's GUID is not checked.
            for (int i = 0; i < before.Length; i++)
                if (i != 1) Assert.AreEqual(before[i], after[i], $"output {i}");
            Assert.AreEqual(recipe.normalMap, recipe.material.GetTexture("_BumpMap"));
        }

        [Test]
        public void Regenerate_FindsMovedOutputThroughTheRecipe() {
            var recipe = Recipe("Plaster");
            HotspotGenerator.Generate(recipe);
            var guid = Guid(recipe.maskMap);
            Assert.IsEmpty(AssetDatabase.MoveAsset(AssetDatabase.GetAssetPath(recipe.maskMap), Folder + "/Moved_Mask.png"));

            HotspotGenerator.Generate(recipe);
            Assert.AreEqual(guid, Guid(recipe.maskMap));
            Assert.AreEqual(Folder + "/Moved_Mask.png", AssetDatabase.GetAssetPath(recipe.maskMap));
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Plaster/Plaster_Mask.png"));
        }

        [Test]
        public void TwoRecipesSameLayout_ProduceIdenticalRects() {
            var a = Recipe("Plaster");
            var b = Recipe("Brick");
            b.seed = 99;
            b.wear.intensity = 1f;
            HotspotGenerator.Generate(a);
            HotspotGenerator.Generate(b);
            Assert.AreEqual(a.hotspotTexture.PatchCount, b.hotspotTexture.PatchCount);
            for (int i = 0; i < a.hotspotTexture.PatchCount; i++)
                Assert.AreEqual(a.hotspotTexture.GetUVRect(i), b.hotspotTexture.GetUVRect(i));
            Assert.AreEqual(a.hotspotTexture.EffectiveTexelsPerUnit, b.hotspotTexture.EffectiveTexelsPerUnit);
        }
        [Test]
        public void DuplicatedRecipe_DoesNotOverwriteTheOriginalsOutputs() {
            var original = Recipe("Plaster");
            HotspotGenerator.Generate(original);
            var originalGuids = Guids(original);
            var originalPaths = System.Array.ConvertAll(originalGuids, AssetDatabase.GUIDToAssetPath);
            var originalBytes = System.IO.File.ReadAllBytes(originalPaths[0]);

            Assert.IsTrue(AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(original), Folder + "/Plaster Old.asset"));
            var copy = AssetDatabase.LoadAssetAtPath<HotspotRecipe>(Folder + "/Plaster Old.asset");
            copy.wear.intensity = 1f;
            copy.seed = 42;
            HotspotGenerator.Generate(copy);

            CollectionAssert.AreEqual(originalGuids, Guids(original));
            CollectionAssert.AreEqual(originalBytes, System.IO.File.ReadAllBytes(originalPaths[0]), "the original base colour was overwritten");
            Assert.AreEqual(Folder + "/Plaster Old/Plaster Old_BaseColor.png", AssetDatabase.GetAssetPath(copy.baseColorMap));
            Assert.AreNotEqual(original.material, copy.material);
            Assert.AreNotEqual(original.hotspotTexture, copy.hotspotTexture);
        }

        [Test]
        public void ForeignTextureInOutputField_IsNotOverwritten() {
            var recipe = Recipe("Plaster");
            var path = Folder + "/Foreign.jpg";
            var tex = new Texture2D(4, 4);
            System.IO.File.WriteAllBytes(path, tex.EncodeToJPG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var bytes = System.IO.File.ReadAllBytes(path);
            recipe.baseColorMap = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

            HotspotGenerator.Generate(recipe);
            CollectionAssert.AreEqual(bytes, System.IO.File.ReadAllBytes(path));
            Assert.AreEqual(Folder + "/Plaster/Plaster_BaseColor.png", AssetDatabase.GetAssetPath(recipe.baseColorMap));
        }

        [Test]
        public void Regenerate_KeepsTheUsersShader() {
            var recipe = Recipe("Plaster");
            HotspotGenerator.Generate(recipe);
            recipe.material.shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            HotspotGenerator.Generate(recipe);
            Assert.AreEqual("Universal Render Pipeline/Simple Lit", recipe.material.shader.name);
            Assert.AreEqual(recipe.baseColorMap, recipe.material.GetTexture("_BaseMap"));
        }
        [Test]
        public void Regenerate_AfterMovingTheOutputFolder_DoesNotRecreateIt() {
            var recipe = Recipe("Plaster");
            HotspotGenerator.Generate(recipe);
            AssetDatabase.CreateFolder(Folder, "Elsewhere");
            Assert.IsEmpty(AssetDatabase.MoveAsset(Folder + "/Plaster", Folder + "/Elsewhere/Plaster"));

            HotspotGenerator.Generate(recipe);
            Assert.IsFalse(AssetDatabase.IsValidFolder(Folder + "/Plaster"), "an empty default folder was created");
            StringAssert.StartsWith(Folder + "/Elsewhere/Plaster/", AssetDatabase.GetAssetPath(recipe.baseColorMap));
        }
    }
}
