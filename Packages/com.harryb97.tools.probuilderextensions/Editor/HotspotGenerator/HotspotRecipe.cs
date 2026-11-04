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

        [Tooltip("How differently each patch samples the layers. 0 = the layers run continuously across the atlas; 1 = every patch takes a random offset (and a 90° turn when the patch allows rotation).")]
        [Range(0, 1)] public float variation = 0.5f;

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

        [Tooltip("Real-world width (metres) covered by one repeat of the albedo, e.g. 1 for a texture showing 1 m of brick wall.")]
        [Min(0.01f)] public float tileSize = 2f;

        [Tooltip("Start the layer at the bottom-left corner of every patch, without random offset or rotation, so each patch begins with a whole brick course or plank.")]
        public bool alignToPatches;

        [Range(0, 2)] public float normalStrength = 1f;
    }

    public enum HotspotWearStyle { Cracks, Bites, None }

    [Serializable]
    public class HotspotWear : ISerializationCallbackReceiver {
        /// <summary>Depth of a Bites band per metre of Wear Size at full Intensity.</summary>
        public const float BitesDepth = 1.25f;

        [Tooltip("Damage added on top of the rim. Cracks: fine cracks branching in from the edge (tiles, plaster). Bites: rounded chips revealing the bottom layer (metal, chipped paint). None: only the rim and the texture below.")]
        public HotspotWearStyle style = HotspotWearStyle.Cracks;

        [Tooltip("Amount of damage from the style and the wear texture. 0 = only the rim; 1 = long cracks or deep bites. Does not change the rim.")]
        [Range(0, 1)] public float intensity = 0.2f;

        [Tooltip("Width of the rounded bevel around each patch, in metres.")]
        [Min(0.001f)] public float bevelWidth = 0.02f;

        [Tooltip("Minimum width (metres) of the damaged rim that runs along the whole border of every patch, revealing the bottom layer. Never narrower than this anywhere. 0 = clean bevel.")]
        [Min(0)] public float edgeWidth = 0.01f;

        [Tooltip("How much lighter worn edges look.")]
        [Range(0, 1)] public float edgeTone = 0.15f;

        [Tooltip("Size of the wear pattern in metres. Cracks: typical spacing between cracks. Bites: typical length of a bite along the edge.")]
        [Min(0.005f)] public float wearSize = 0.05f;

        [Tooltip("Optional grunge frame: white = intact, black = worn, with a ragged black border. Only a strip of its left side is read and wrapped along every patch border; loose specks are removed.")]
        public Texture2D wearTexture;

        [Tooltip("Fraction of the wear texture's width read as the border strip.")]
        [Range(0.01f, 0.5f)] public float textureBorder = 0.2f;

        [Tooltip("How far (metres) the wear texture reaches into every patch at full Intensity.")]
        [Min(0.001f)] public float textureDepth = 0.05f;

        [Tooltip("How much darker gaps, cracks, worn areas and the foot of the bevel look, so edges read even when both layers are the same material.")]
        [Range(0, 1)] public float cavity = 0.5f;

        [Tooltip("Depth of the gaps and worn areas relative to the bevel.")]
        [Range(0.05f, 1)] public float gapDepth = 0.5f;

        // Settings of recipes saved before 1.2. -1 = not present.
        [SerializeField, HideInInspector] private float chipSize = -1f;
        [SerializeField, HideInInspector] private float chipDensity = -1f;

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        void ISerializationCallbackReceiver.OnAfterDeserialize() {
            if (chipSize < 0) return;
            style = HotspotWearStyle.Bites;
            wearSize = chipSize;
            edgeWidth = 0f;
            intensity = Mathf.Min(1f, intensity * (0.5f + 1.5f * Mathf.Max(0f, chipDensity)) / BitesDepth);
            chipSize = chipDensity = -1f;
        }
    }
}
