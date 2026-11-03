using System;
using HarryTools.ProBuilderExtensions.HotspotUV;
using UnityEngine;

namespace HarryTools.ProBuilderExtensions.Generator {
    /// <summary>Everything needed to (re)generate a hotspot atlas, plus references to the generated outputs.</summary>
    [CreateAssetMenu(fileName = "New Hotspot Recipe", menuName = "HarryTools/ProBuilder Extensions/Hotspot Recipe", order = 302)]
    public class HotspotRecipe : ScriptableObject {
        [Tooltip("Patches to generate. Every recipe using the same layout produces the same rects, so materials can be swapped without remapping.")]
        public HotspotLayout layout;

        [Tooltip("Surface layer: paint, plaster, varnish…")]
        public HotspotLayer top = new();

        [Tooltip("Layer revealed in the gaps between patches and where the top layer is chipped: brick, bare wood, mortar…")]
        public HotspotLayer bottom = new();

        public HotspotWear wear = new();

        [Tooltip("Output texture width in pixels. The height follows the layout's aspect.")]
        public int resolution = 4096;

        [Tooltip("World width (metres) of the atlas, overriding the layout's. 0 = use the layout's.")]
        [Min(0)] public float worldWidthOverride;

        [Tooltip("Seed for the wear pattern and the per-patch variation.")]
        public int seed;

        [Header("Outputs (written by Generate)")]
        public Texture2D baseColorMap;
        public Texture2D normalMap;
        public Texture2D maskMap;
        public Texture2D occlusionMap;
        public Material material;
        public HotspotTexture hotspotTexture;

        public float WorldWidth => worldWidthOverride > 0 ? worldWidthOverride : layout != null ? layout.worldWidth : 0f;
    }

    [Serializable]
    public class HotspotLayer {
        [Tooltip("Tileable colour texture.")]
        public Texture2D albedo;

        [Tooltip("Optional tileable normal map.")]
        public Texture2D normal;

        [Tooltip("Optional. R = metallic, A = smoothness (URP layout). Overrides the values below.")]
        public Texture2D metallicSmoothness;

        public Color tint = Color.white;
        [Range(0, 1)] public float metallic;
        [Range(0, 1)] public float smoothness = 0.3f;

        [Tooltip("Pixels of the albedo per metre. Sets how big the layer looks.")]
        [Min(1)] public float texelsPerUnit = 512f;

        [Range(0, 2)] public float normalStrength = 1f;
    }

    [Serializable]
    public class HotspotWear {
        [Tooltip("0 = only a soft bevel and a slight edge tone; 1 = large chips revealing the bottom layer.")]
        [Range(0, 1)] public float intensity = 0.2f;

        [Tooltip("Width of the bevel around each patch, in metres.")]
        [Min(0.001f)] public float bevelWidth = 0.02f;

        [Tooltip("How much lighter worn edges look.")]
        [Range(0, 1)] public float edgeTone = 0.15f;

        [Tooltip("Typical size of a chip, in metres.")]
        [Min(0.005f)] public float chipSize = 0.05f;

        [Tooltip("How far into the patch chips reach.")]
        [Range(0, 1)] public float chipDensity = 0.5f;

        [Tooltip("Depth of the gaps and chips relative to the bevel.")]
        [Range(0.05f, 1)] public float gapDepth = 0.5f;
    }
}
