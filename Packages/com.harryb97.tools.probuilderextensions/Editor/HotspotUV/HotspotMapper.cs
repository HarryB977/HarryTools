using UnityEngine;

namespace HarryTools.ProBuilderExtensions.HotspotUV.Editor {
    /// <summary>Turns a face's planar projection into UVs inside a hotspot patch.</summary>
    public static class HotspotMapper {
        /// <param name="projected">Face points in world units, already aligned to their tightest bounding box.</param>
        /// <param name="uvsOut">Receives one UV per point. Left untouched when the face is degenerate.</param>
        /// <returns>False when the face has no area or the input is invalid.</returns>
        public static bool Map(Vector2[] projected, HotspotTexture hotspot, HotspotMatch match, bool preserveAspect, Vector2[] uvsOut) {
            if (projected == null || uvsOut == null || uvsOut.Length < projected.Length || projected.Length < 3) return false;
            if (hotspot == null || !match.IsValid || match.index >= hotspot.PatchCount) return false;

            var points = (Vector2[])projected.Clone();
            if (match.rotated)
                for (int i = 0; i < points.Length; i++)
                    points[i] = new Vector2(-points[i].y, points[i].x);

            GetBounds(points, out var min, out var size);
            if (!(size.x > 1e-6f) || !(size.y > 1e-6f) || float.IsInfinity(size.x) || float.IsInfinity(size.y)) return false;

            var texSize = hotspot.TextureSize;
            var tpu = hotspot.EffectiveTexelsPerUnit;
            var tiling = tpu > 0 ? match.tiling : TilingAxis.None;
            var rect = Inset(hotspot.GetUVRect(match.index), hotspot.GetPatch(match.index).inset, texSize, tiling);

            var result = new Vector2[points.Length];
            for (int i = 0; i < points.Length; i++) {
                var local = points[i] - min;
                result[i] = tiling switch {
                    TilingAxis.Horizontal => new Vector2(local.x * tpu / texSize.x, rect.yMin + local.y / size.y * rect.height),
                    TilingAxis.Vertical => new Vector2(rect.xMin + local.x / size.x * rect.width, rect.yMin + local.y * tpu / texSize.y),
                    _ => Vector2.zero,
                };
            }

            if (tiling == TilingAxis.None) {
                var scale = new Vector2(rect.width / size.x, rect.height / size.y);
                var offset = Vector2.zero;
                if (preserveAspect) {
                    // Uniform scale in texel space, centred inside the rect.
                    var texelScale = Mathf.Min(scale.x * texSize.x, scale.y * texSize.y);
                    scale = new Vector2(texelScale / texSize.x, texelScale / texSize.y);
                    offset = (rect.size - Vector2.Scale(size, scale)) * 0.5f;
                }
                for (int i = 0; i < points.Length; i++)
                    result[i] = rect.min + offset + Vector2.Scale(points[i] - min, scale);
            }

            if (match.mirrorX || match.mirrorY) {
                GetBounds(result, out var uvMin, out var uvSize);
                var sum = uvMin * 2 + uvSize;
                for (int i = 0; i < result.Length; i++) {
                    if (match.mirrorX) result[i].x = sum.x - result[i].x;
                    if (match.mirrorY) result[i].y = sum.y - result[i].y;
                }
            }

            System.Array.Copy(result, uvsOut, result.Length);
            return true;
        }

        /// <summary>Shrinks the rect by <paramref name="inset"/> texels per side, except along the tiling axis so repeats stay seamless.</summary>
        private static Rect Inset(Rect rect, int inset, Vector2 texSize, TilingAxis tiling) {
            if (inset <= 0) return rect;
            var dx = tiling == TilingAxis.Horizontal ? 0f : Mathf.Min(inset / texSize.x, rect.width * 0.25f);
            var dy = tiling == TilingAxis.Vertical ? 0f : Mathf.Min(inset / texSize.y, rect.height * 0.25f);
            return Rect.MinMaxRect(rect.xMin + dx, rect.yMin + dy, rect.xMax - dx, rect.yMax - dy);
        }

        private static void GetBounds(Vector2[] points, out Vector2 min, out Vector2 size) {
            min = points[0];
            var max = points[0];
            foreach (var p in points) {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            size = max - min;
        }
    }
}
