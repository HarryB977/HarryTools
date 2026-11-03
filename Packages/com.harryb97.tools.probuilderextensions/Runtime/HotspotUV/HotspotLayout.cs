using System.Collections.Generic;
using UnityEngine;

namespace HarryTools.ProBuilderExtensions.HotspotUV {
    /// <summary>
    /// A resolution-independent set of hotspot patches. The same layout is used to generate an atlas and to map faces
    /// onto it, so both always match.
    /// </summary>
    [CreateAssetMenu(fileName = "New Hotspot Layout", menuName = "HarryTools/ProBuilder Extensions/Hotspot Layout", order = 301)]
    public class HotspotLayout : ScriptableObject {
        private const float Epsilon = 1e-4f;

        [Tooltip("Width:height ratio of the atlas.")]
        public Vector2Int aspect = new(1, 1);

        [Tooltip("World width (metres) covered by the whole atlas. Sets the texel density: texture width / world width.")]
        [Min(0.01f)] public float worldWidth = 8f;

        [Tooltip("Texture width the layout was designed for.")]
        public int recommendedResolution = 4096;

        public List<HotspotPatch> patches = new();

        public Vector2Int GetTextureSize(int width) {
            var ratio = aspect.x > 0 && aspect.y > 0 ? (float)aspect.y / aspect.x : 1f;
            return new Vector2Int(width, Mathf.Max(1, Mathf.RoundToInt(width * ratio)));
        }

        public Rect GetPixelRect(int index, Vector2 textureSize) {
            var r = patches[index].rect;
            return new Rect(r.x * textureSize.x, r.y * textureSize.y, r.width * textureSize.x, r.height * textureSize.y);
        }

        public float GetTexelsPerUnit(int textureWidth) => worldWidth > 0 ? textureWidth / worldWidth : 0f;

        public TilingAxis GetTilingAxis(int index) {
            var p = patches[index];
            if (!p.allowTiling) return TilingAxis.None;
            if (SpansFully(p.rect.xMin, p.rect.xMax)) return TilingAxis.Horizontal;
            if (SpansFully(p.rect.yMin, p.rect.yMax)) return TilingAxis.Vertical;
            return TilingAxis.None;
        }

        /// <summary>Adds a message to <paramref name="errors"/> for every invalid patch.</summary>
        public bool Validate(List<string> errors) {
            var count = errors.Count;
            if (worldWidth <= 0) errors.Add("World Width must be greater than 0.");
            if (aspect.x <= 0 || aspect.y <= 0) errors.Add("Aspect must be positive.");

            for (int i = 0; i < patches.Count; i++) {
                var r = patches[i].rect;
                if (r.width <= 0 || r.height <= 0)
                    errors.Add($"Patch {i} has zero size.");
                else if (r.xMin < -Epsilon || r.yMin < -Epsilon || r.xMax > 1 + Epsilon || r.yMax > 1 + Epsilon)
                    errors.Add($"Patch {i} is outside the texture.");
                else if (patches[i].allowTiling && GetTilingAxis(i) == TilingAxis.None)
                    errors.Add($"Patch {i} allows tiling but does not span the full width or height.");
            }
            return errors.Count == count;
        }

        private static bool SpansFully(float min, float max) => min <= Epsilon && max >= 1 - Epsilon;
    }
}
