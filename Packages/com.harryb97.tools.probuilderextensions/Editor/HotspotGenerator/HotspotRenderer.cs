using System;
using System.Collections.Generic;
using HarryTools.ProBuilderExtensions.HotspotUV;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HarryTools.ProBuilderExtensions.Generator {
    public enum HotspotMap { BaseColor, Normal, Mask, Occlusion, Height, Wear, Cracks }

    /// <summary>Renders the maps of a <see cref="HotspotRecipe"/> on the GPU.</summary>
    public sealed class HotspotRenderer : IDisposable {
        public const int MaxPatches = 256;
        private const string ShaderName = "Hidden/HarryTools/HotspotGenerator";
        private const int DataPass = 0, ChannelPass = 5, CracksPass = 6;

        private static readonly int DataTexId = Shader.PropertyToID("_DataTex");

        private readonly Material material;
        private readonly Dictionary<HotspotMap, RenderTexture> targets = new();
        private RenderTexture data;
        private Texture2D wearMask;

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
            material.SetFloat("_Style", (int)wear.style);
            material.SetFloat("_WearSize", Mathf.Max(0.005f, wear.wearSize));
            material.SetFloat("_EdgeWidth", Mathf.Max(0f, wear.edgeWidth));
            material.SetFloat("_GapDepth", wear.gapDepth);
            material.SetFloat("_Cavity", wear.cavity);
            SetWearMask(wear, worldSize);
            material.SetFloat("_Seed", recipe.seed);
            material.SetFloat("_Variation", Mathf.Clamp01(recipe.variation));

            SetLayer("_Top", recipe.top, worldSize);
            SetLayer("_Bottom", recipe.bottom, worldSize);
        }

        private void SetWearMask(HotspotWear wear, Vector2 worldSize) {
            if (wear.wearTexture == null) {
                material.SetTexture("_WearMask", Texture2D.blackTexture);
                material.SetFloat("_HasWearMask", 0);
                return;
            }
            wearMask = HotspotWearMask.Build(wear.wearTexture, wear.textureBorder);
            var depth = Mathf.Max(0.001f, wear.textureDepth);
            // The strip keeps its proportions: its length along the border follows from its depth, rounded to whole
            // repetitions of the atlas so tiling strips wrap seamlessly.
            var stripLength = depth * wearMask.height / wearMask.width;
            var reps = new Vector2(Mathf.Max(1, Mathf.Round(worldSize.x / stripLength)), Mathf.Max(1, Mathf.Round(worldSize.y / stripLength)));
            material.SetTexture("_WearMask", wearMask);
            material.SetFloat("_HasWearMask", 1);
            material.SetFloat("_TextureDepth", depth);
            material.SetVector("_WearMaskReps", new Vector4(reps.x, reps.y, 0, 0));
        }

        private void SetLayer(string prefix, HotspotLayer layer, Vector2 worldSize) {
            var albedo = layer.albedo != null ? (Texture)layer.albedo : Texture2D.whiteTexture;
            material.SetTexture(prefix + "Albedo", albedo);
            material.SetTexture(prefix + "Normal", layer.normal != null ? layer.normal : Texture2D.normalTexture);
            material.SetTexture(prefix + "MS", layer.metallicSmoothness != null ? layer.metallicSmoothness : Texture2D.whiteTexture);
            material.SetColor(prefix + "Tint", layer.tint);

            // Whole repetitions across the atlas, so tiling strips wrap seamlessly.
            var tileWorld = new Vector2(1f, (float)albedo.height / Mathf.Max(1, albedo.width)) * Mathf.Max(0.01f, layer.tileSize);
            var reps = ComputeRepetitions(worldSize, tileWorld);
            var texelsPerAtlasPixel = albedo.width * reps.x / Size.x;
            var lod = Mathf.Log(Mathf.Max(1f, texelsPerAtlasPixel), 2f);

            material.SetVector(prefix + "Params", new Vector4(layer.metallic, layer.smoothness, layer.normalStrength, lod));
            material.SetVector(prefix + "Tiling", new Vector4(reps.x, reps.y, layer.normal != null ? 1 : 0, layer.metallicSmoothness != null ? 1 : 0));
            material.SetFloat(prefix + "Align", layer.alignToPatches ? 1 : 0);
        }

        /// <summary>Whole numbers of layer tiles across the atlas on each axis.</summary>
        /// <remarks>
        /// Both counts must be integers for the atlas to wrap seamlessly, so the tile is resized slightly; the pair is
        /// chosen to keep the tile's proportions (no stretching) first and its size second.
        /// </remarks>
        public static Vector2 ComputeRepetitions(Vector2 worldSize, Vector2 tileWorld) {
            tileWorld = Vector2.Max(tileWorld, new Vector2(1e-4f, 1e-4f));
            var ideal = worldSize.x / tileWorld.x;
            var ratio = (worldSize.y / tileWorld.y) / ideal; // vertical repetitions per horizontal one
            var best = new Vector2(Mathf.Max(1, Mathf.Round(ideal)), Mathf.Max(1, Mathf.Round(worldSize.y / tileWorld.y)));
            var bestCost = float.MaxValue;
            var max = Mathf.Max(4, Mathf.CeilToInt(ideal * 2) + 2);
            for (int x = 1; x <= max; x++) {
                var y = Mathf.Max(1, Mathf.Round(x * ratio));
                var tile = new Vector2(worldSize.x / x, worldSize.y / y);
                var anisotropy = Mathf.Abs(Mathf.Log((tile.x / tile.y) / (tileWorld.x / tileWorld.y)));
                var scale = Mathf.Abs(Mathf.Log(tile.x / tileWorld.x));
                var cost = anisotropy * 4f + scale;
                if (cost < bestCost) {
                    bestCost = cost;
                    best = new Vector2(x, y);
                }
            }
            return best;
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
                Blit(data, DataPass);
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
                    Blit(rt, ChannelPass);
                    break;
                case HotspotMap.Cracks:
                    Blit(rt, CracksPass);
                    break;
                default:
                    Blit(rt, 1 + (int)map);
                    break;
            }
            targets[map] = rt;
            return rt;
        }

        /// <summary>Blits a pass without leaving <see cref="RenderTexture.active"/> changed (IMGUI draws into it).</summary>
        private void Blit(RenderTexture target, int pass) {
            var previous = RenderTexture.active;
            Graphics.Blit(Texture2D.blackTexture, target, material, pass);
            RenderTexture.active = previous;
        }

        /// <summary>Returns a readable copy of the map. The caller owns (and must destroy) the texture.</summary>
        public Texture2D ReadBack(HotspotMap map, bool linear) {
            var previous = RenderTexture.active;
            var rt = Render(map);
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
            if (wearMask != null) Object.DestroyImmediate(wearMask);
            wearMask = null;
            if (material != null) Object.DestroyImmediate(material);
        }

        private static void Release(RenderTexture rt) {
            if (rt == null) return;
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }
}
