using System;
using System.Collections.Generic;
using System.IO;
using HarryTools.ProBuilderExtensions.HotspotUV;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HarryTools.ProBuilderExtensions.Generator {
    /// <summary>
    /// Writes the maps of a <see cref="HotspotRecipe"/> as textures and creates or updates a URP Lit material and a
    /// <see cref="HotspotTexture"/>. Existing outputs are overwritten in place, so their GUIDs (and every reference to
    /// them) survive regeneration.
    /// </summary>
    public static class HotspotGenerator {
        private const string LitShader = "Universal Render Pipeline/Lit";

        /// <summary>Default folder for the outputs: a subfolder next to the recipe, named after it.</summary>
        public static string GetOutputFolder(HotspotRecipe recipe) {
            var path = AssetDatabase.GetAssetPath(recipe);
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("The recipe must be saved as an asset.", nameof(recipe));
            return $"{Path.GetDirectoryName(path)!.Replace('\\', '/')}/{recipe.name}";
        }

        public static void Generate(HotspotRecipe recipe) {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            if (recipe.layout == null) throw new InvalidOperationException($"Hotspot Recipe '{recipe.name}' has no layout.");

            var errors = new List<string>();
            if (!recipe.layout.Validate(errors))
                Debug.LogWarning($"Hotspot Generator: layout '{recipe.layout.name}' has problems:\n{string.Join("\n", errors)}");

            var folder = GetOutputFolder(recipe);
            var name = recipe.name;
            var baseColorPath = Resolve(recipe, recipe.baseColorMap, $"{folder}/{name}_BaseColor.png");
            var normalPath = Resolve(recipe, recipe.normalMap, $"{folder}/{name}_Normal.png");
            var maskPath = Resolve(recipe, recipe.maskMap, $"{folder}/{name}_Mask.png");
            var occlusionPath = Resolve(recipe, recipe.occlusionMap, $"{folder}/{name}_Occlusion.png");
            var materialPath = Resolve(recipe, recipe.material, $"{folder}/{name}.mat");
            var hotspotPath = Resolve(recipe, recipe.hotspotTexture, $"{folder}/{name} Hotspot.asset");

            // Only create the default folder if an output actually goes there (they may have been moved elsewhere).
            foreach (var path in new[] { baseColorPath, normalPath, maskPath, occlusionPath, materialPath, hotspotPath })
                if (path.StartsWith(folder + "/")) { EnsureFolder(folder); break; }

            using (var renderer = new HotspotRenderer(recipe, recipe.resolution)) {
                recipe.baseColorMap = WriteMap(renderer, HotspotMap.BaseColor, baseColorPath, true, false);
                recipe.normalMap = WriteMap(renderer, HotspotMap.Normal, normalPath, false, true);
                recipe.maskMap = WriteMap(renderer, HotspotMap.Mask, maskPath, false, false);
                recipe.occlusionMap = WriteMap(renderer, HotspotMap.Occlusion, occlusionPath, false, false);
            }

            recipe.material = WriteMaterial(recipe, materialPath);
            recipe.hotspotTexture = WriteHotspotTexture(recipe, hotspotPath);

            EditorUtility.SetDirty(recipe);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// The path of the existing output when it can be overwritten: it still exists, has the expected file type, is
        /// a main asset and no other recipe references it (a duplicated recipe starts with the original's outputs).
        /// Otherwise the default path.
        /// </summary>
        private static string Resolve(HotspotRecipe recipe, Object existing, string defaultPath) {
            if (existing == null || !AssetDatabase.IsMainAsset(existing)) return defaultPath;
            var path = AssetDatabase.GetAssetPath(existing);
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/") ||
                !string.Equals(Path.GetExtension(path), Path.GetExtension(defaultPath), StringComparison.OrdinalIgnoreCase))
                return defaultPath;
            return IsUsedByAnotherRecipe(recipe, existing) ? defaultPath : path;
        }

        private static bool IsUsedByAnotherRecipe(HotspotRecipe recipe, Object output) {
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(HotspotRecipe))) {
                var other = AssetDatabase.LoadAssetAtPath<HotspotRecipe>(AssetDatabase.GUIDToAssetPath(guid));
                if (other == null || other == recipe) continue;
                if (other.baseColorMap == output || other.normalMap == output || other.maskMap == output ||
                    other.occlusionMap == output || other.material == output || other.hotspotTexture == output)
                    return true;
            }
            return false;
        }

        private static string EnsureFolder(string folder) {
            if (AssetDatabase.IsValidFolder(folder)) return folder;
            var parent = Path.GetDirectoryName(folder)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
            return folder;
        }

        private static Texture2D WriteMap(HotspotRenderer renderer, HotspotMap map, string path, bool sRGB, bool normal) {
            var tex = renderer.ReadBack(map, linear: !sRGB);
            try {
                File.WriteAllBytes(path, tex.EncodeToPNG());
            } finally {
                Object.DestroyImmediate(tex);
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = sRGB;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            var largest = Mathf.Max(renderer.Size.x, renderer.Size.y);
            if (importer.maxTextureSize < largest) importer.maxTextureSize = Mathf.Min(16384, Mathf.NextPowerOfTwo(largest));
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Material WriteMaterial(HotspotRecipe recipe, string path) {
            var shader = Shader.Find(LitShader);
            if (shader == null) {
                Debug.LogError($"Hotspot Generator: shader '{LitShader}' not found; the material was not created. Is URP installed?");
                return recipe.material;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) {
                material = new Material(shader);
                material.SetColor("_BaseColor", Color.white);
                material.SetFloat("_Smoothness", 1f); // the mask's alpha holds the real smoothness
                AssetDatabase.CreateAsset(material, path);
            }
            // An existing material keeps its shader: the user may have switched to one with the same properties.

            material.SetTexture("_BaseMap", recipe.baseColorMap);
            material.SetTexture("_MainTex", recipe.baseColorMap);
            material.SetTexture("_BumpMap", recipe.normalMap);
            material.SetTexture("_MetallicGlossMap", recipe.maskMap);
            material.SetTexture("_OcclusionMap", recipe.occlusionMap);
            material.SetFloat("_SmoothnessTextureChannel", 0);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            material.EnableKeyword("_OCCLUSIONMAP");
            EditorUtility.SetDirty(material);
            return material;
        }

        private static HotspotTexture WriteHotspotTexture(HotspotRecipe recipe, string path) {
            var hotspot = AssetDatabase.LoadAssetAtPath<HotspotTexture>(path);
            if (hotspot == null) {
                hotspot = ScriptableObject.CreateInstance<HotspotTexture>();
                AssetDatabase.CreateAsset(hotspot, path);
            }
            hotspot.target = recipe.baseColorMap;
            hotspot.layout = recipe.layout;
            hotspot.worldWidthOverride = recipe.worldWidthOverride;
            hotspot.texelsPerUnit = hotspot.EffectiveTexelsPerUnit;
            EditorUtility.SetDirty(hotspot);
            return hotspot;
        }
    }
}
