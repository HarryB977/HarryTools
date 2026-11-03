using System;
using System.Collections.Generic;
using HarryTools.ProBuilderExtensions.HotspotUV;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HarryTools.ProBuilderExtensions.Generator {
    public enum HotspotMap { BaseColor, Normal, Mask, Occlusion, Height, Wear }

    /// <summary>Renders the maps of a <see cref="HotspotRecipe"/> on the GPU.</summary>
    public sealed class HotspotRenderer : IDisposable {
        public const int MaxPatches = 256;
        private const string ShaderName = "Hidden/HarryTools/HotspotGenerator";
        private const int DataPass = 0, ChannelPass = 5;

        private static readonly int DataTexId = Shader.PropertyToID("_DataTex");

        private readonly Material material;
        private readonly Dictionary<HotspotMap, RenderTexture> targets = new();
        private RenderTexture data;

        public Vector2Int Size { get; }

        public HotspotRenderer(HotspotRecipe recipe, int width) {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            if (recipe.layout == null) throw new ArgumentException("The recipe has no layout.", nameof(recipe));

            var shader = Shader.Find(ShaderName);
            if (shader == null) throw new InvalidOperationException($"Shader '{ShaderName}' not found.");
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };

            Size = recipe.layout.GetTextureSize(Mathf.Max(1, width));
            Configure(recipe);
        }

        private void Configure(HotspotRecipe recipe) {
            var layout = recipe.layout;
            var worldWidth = recipe.WorldWidth > 0 ? recipe.WorldWidth : 1f;
            var metresPerPixel = worldWidth / Size.x;
            var worldSize = new Vector2(worldWidth, Size.y * metresPerPixel);
            material.SetVector("_AtlasWorld", new Vector4(worldSize.x, worldSize.y, metresPerPixel, 0));

            var count = Mathf.Min(layout.patches.Count, MaxPatches);
            if (layout.patches.Count > MaxPatches)
                Debug.LogWarning($"Hotspot Generator: only the first {MaxPatches} patches of '{layout.name}' are generated.");

            var rects = new Vector4[MaxPatches];
            var flags = new Vector4[MaxPatches];
            var random = new System.Random(recipe.seed);
            for (int i = 0; i < count; i++) {
                var p = layout.patches[i];
                var r = p.rect.Normalized();
                rects[i] = new Vector4(r.xMin, r.yMin, r.xMax, r.yMax);
                flags[i] = new Vector4((int)layout.GetTilingAxis(i), p.allowRotation ? 1 : 0, (float)random.NextDouble(), (float)random.NextDouble());
            }
            material.SetInt("_PatchCount", count);
            material.SetVectorArray("_Patches", rects);
            material.SetVectorArray("_PatchFlags", flags);

            var wear = recipe.wear;
            material.SetFloat("_Intensity", wear.intensity);
            material.SetFloat("_BevelWidth", Mathf.Max(0.001f, wear.bevelWidth));
            material.SetFloat("_EdgeTone", wear.edgeTone);
            material.SetFloat("_ChipSize", Mathf.Max(0.005f, wear.chipSize));
            material.SetFloat("_ChipDensity", wear.chipDensity);
            material.SetFloat("_GapDepth", wear.gapDepth);
            material.SetFloat("_Seed", recipe.seed);

            SetLayer("_Top", recipe.top, worldSize);
            SetLayer("_Bottom", recipe.bottom, worldSize);
        }

        private void SetLayer(string prefix, HotspotLayer layer, Vector2 worldSize) {
            var albedo = layer.albedo != null ? (Texture)layer.albedo : Texture2D.whiteTexture;
            material.SetTexture(prefix + "Albedo", albedo);
            material.SetTexture(prefix + "Normal", layer.normal != null ? layer.normal : Texture2D.normalTexture);
            material.SetTexture(prefix + "MS", layer.metallicSmoothness != null ? layer.metallicSmoothness : Texture2D.whiteTexture);
            material.SetColor(prefix + "Tint", layer.tint);

            // Whole repetitions across the atlas, so tiling strips wrap seamlessly.
            var tileWorld = albedo.width / Mathf.Max(1f, layer.texelsPerUnit);
            var reps = new Vector2(Mathf.Max(1, Mathf.Round(worldSize.x / tileWorld)), Mathf.Max(1, Mathf.Round(worldSize.y / tileWorld)));
            var texelsPerAtlasPixel = albedo.width * reps.x / Size.x;
            var lod = Mathf.Log(Mathf.Max(1f, texelsPerAtlasPixel), 2f);

            material.SetVector(prefix + "Params", new Vector4(layer.metallic, layer.smoothness, layer.normalStrength, lod));
            material.SetVector(prefix + "Tiling", new Vector4(reps.x, reps.y, layer.normal != null ? 1 : 0, layer.metallicSmoothness != null ? 1 : 0));
        }

        private static bool IsColor(HotspotMap map) => map == HotspotMap.BaseColor;

        private RenderTexture Create(RenderTextureFormat format, bool sRGB) {
            var rt = new RenderTexture(Size.x, Size.y, 0, format, sRGB ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear) {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            rt.Create();
            return rt;
        }

        private RenderTexture Data {
            get {
                if (data != null) return data;
                data = Create(RenderTextureFormat.ARGBHalf, false);
                Graphics.Blit(Texture2D.blackTexture, data, material, DataPass);
                material.SetTexture(DataTexId, data);
                return data;
            }
        }

        public RenderTexture Render(HotspotMap map) {
            if (targets.TryGetValue(map, out var rt)) return rt;
            _ = Data;
            rt = Create(RenderTextureFormat.ARGB32, IsColor(map));
            switch (map) {
                case HotspotMap.Height:
                case HotspotMap.Wear:
                    material.SetInt("_Channel", map == HotspotMap.Height ? 0 : 1);
                    Graphics.Blit(Texture2D.blackTexture, rt, material, ChannelPass);
                    break;
                default:
                    Graphics.Blit(Texture2D.blackTexture, rt, material, 1 + (int)map);
                    break;
            }
            targets[map] = rt;
            return rt;
        }

        /// <summary>Returns a readable copy of the map. The caller owns (and must destroy) the texture.</summary>
        public Texture2D ReadBack(HotspotMap map, bool linear) {
            var rt = Render(map);
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Size.x, Size.y, TextureFormat.RGBA32, false, linear);
            tex.ReadPixels(new Rect(0, 0, Size.x, Size.y), 0, 0, false);
            tex.Apply(false, false);
            RenderTexture.active = previous;
            return tex;
        }

        public void Dispose() {
            foreach (var rt in targets.Values) Release(rt);
            targets.Clear();
            Release(data);
            data = null;
            if (material != null) Object.DestroyImmediate(material);
        }

        private static void Release(RenderTexture rt) {
            if (rt == null) return;
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }
}
