using System.Collections.Generic;
using UnityEngine;

namespace HarryTools.ProBuilderExtensions.HotspotUV {
    [CreateAssetMenu(fileName = "New Hotspot Texture", menuName = "HarryTools/ProBuilder Extensions/Hotspot Texture", order = 300)]
    public class HotspotTexture : ScriptableObject {
        public Texture target;

        [Tooltip("Optional. When set, the patches come from this layout instead of the rects below.")]
        public HotspotLayout layout;

        [Tooltip("With a layout: world width (metres) of the atlas, overriding the layout's. 0 = use the layout's.")]
        [Min(0)] public float worldWidthOverride;

        [Tooltip("Without a layout: texels per world unit, used to pick the hotspot whose size best matches the face. Set to 0 to match only by aspect ratio.")]
        public float texelsPerUnit = 256f;

        // Rects are stored in texture pixels, with the origin at the top-left corner of the texture. Ignored when a layout is set.
        public List<Rect> rects = new List<Rect>();

        private static readonly System.Random SharedRandom = new();

        public int PatchCount => layout != null ? layout.patches.Count : rects?.Count ?? 0;

        public bool IsValid => (target != null || layout != null) && PatchCount > 0;

        public Vector2 TextureSize {
            get {
                if (target != null) return new Vector2(target.width, target.height);
                if (layout != null) return layout.GetTextureSize(layout.recommendedResolution);
                return Vector2.one;
            }
        }

        public float EffectiveTexelsPerUnit {
            get {
                if (layout == null) return texelsPerUnit;
                var width = worldWidthOverride > 0 ? worldWidthOverride : layout.worldWidth;
                return width > 0 ? TextureSize.x / width : 0f;
            }
        }

        public HotspotPatch GetPatch(int index) {
            if (layout != null) return layout.patches[index];
            var size = TextureSize;
            var r = rects[index].Normalized();
            var patch = HotspotPatch.Default(new Rect(r.x / size.x, r.y / size.y, r.width / size.x, r.height / size.y));
            patch.inset = 0; // pixel rects are drawn by hand and were never inset
            return patch;
        }

        /// <summary>Size of the patch in texture pixels.</summary>
        public Vector2 GetPixelSize(int index) {
            if (layout != null) return layout.GetPixelRect(index, TextureSize).size;
            return rects[index].Normalized().size;
        }

        public TilingAxis GetTilingAxis(int index) => layout != null ? layout.GetTilingAxis(index) : TilingAxis.None;

        /// <summary>
        /// Returns the patch at <paramref name="index"/> in UV space (0-1, origin at the bottom-left).
        /// </summary>
        public Rect GetUVRect(int index) {
            var r = GetPatch(index).rect;
            return Rect.MinMaxRect(r.xMin, 1 - r.yMax, r.xMax, 1 - r.yMin);
        }

        /// <summary>
        /// Picks the patch that best matches a face of <paramref name="faceSize"/> (world units).
        /// With a texel density, only patches whose world size is within the scale tolerance on both axes are
        /// candidates; if none is, a tiling strip whose cross size fits is used, and failing that the closest patch
        /// (flagged as out of tolerance). Among candidates scoring within the random tolerance of the best, one is
        /// picked at random.
        /// </summary>
        public HotspotMatch FindBestPatch(Vector2 faceSize, HotspotMatchSettings settings) {
            if (!IsValid || !(faceSize.x > Mathf.Epsilon) || !(faceSize.y > Mathf.Epsilon) ||
                float.IsInfinity(faceSize.x) || float.IsInfinity(faceSize.y))
                return HotspotMatch.Invalid;

            var random = settings.random ?? SharedRandom;
            var tpu = EffectiveTexelsPerUnit;
            var maxLog = Mathf.Log(1f + Mathf.Max(0f, settings.scaleTolerance)) + 1e-5f;

            var all = new List<(int index, bool rotated, float score, bool inTolerance)>();
            var tiling = new List<(int index, bool rotated, float error)>();

            for (int i = 0; i < PatchCount; i++) {
                var size = GetPixelSize(i);
                if (size.x <= 0 || size.y <= 0) continue;
                var canRotate = settings.allowRotation && GetPatch(i).allowRotation;
                var axis = GetTilingAxis(i);

                for (int o = 0; o < (canRotate ? 2 : 1); o++) {
                    var rectSize = o == 0 ? size : new Vector2(size.y, size.x);
                    var inTolerance = true;
                    // The scale tolerance only applies to layouts; hand-drawn rects keep the original best-match behaviour.
                    if (tpu > 0 && layout != null) {
                        var world = rectSize / tpu;
                        inTolerance = Mathf.Abs(Mathf.Log(faceSize.x / world.x)) <= maxLog &&
                                      Mathf.Abs(Mathf.Log(faceSize.y / world.y)) <= maxLog;

                        if (axis != TilingAxis.None) {
                            // The cross axis is the one that does not repeat, measured in face space.
                            var repeatsAlongFaceX = (axis == TilingAxis.Horizontal) != (o == 1);
                            var crossRatio = repeatsAlongFaceX ? faceSize.y / world.y : faceSize.x / world.x;
                            var error = Mathf.Abs(Mathf.Log(crossRatio));
                            if (error <= maxLog) tiling.Add((i, o == 1, error));
                        }
                    }
                    all.Add((i, o == 1, Score(faceSize, rectSize, tpu), inTolerance));
                }
            }

            if (all.Count == 0) return HotspotMatch.Invalid;

            var candidates = all.FindAll(c => c.inTolerance);
            int index;
            bool rotated, outOfTolerance = false;

            if (candidates.Count > 0) {
                var best = float.MaxValue;
                foreach (var c in candidates) best = Mathf.Min(best, c.score);
                candidates = candidates.FindAll(c => c.score <= best + settings.randomTolerance);
                var pick = candidates[random.Next(candidates.Count)];
                (index, rotated) = (pick.index, pick.rotated);
            } else if (tiling.Count > 0) {
                var best = tiling[0];
                foreach (var t in tiling) if (t.error < best.error) best = t;
                (index, rotated) = (best.index, best.rotated);
            } else {
                var best = all[0];
                foreach (var c in all) if (c.score < best.score) best = c;
                (index, rotated, outOfTolerance) = (best.index, best.rotated, true);
            }

            var chosen = GetPatch(index);
            return new HotspotMatch {
                index = index,
                rotated = rotated,
                tiling = GetTilingAxis(index),
                outOfTolerance = outOfTolerance,
                mirrorX = settings.allowMirror && chosen.allowMirrorX && random.Next(2) == 1,
                mirrorY = settings.allowMirror && chosen.allowMirrorY && random.Next(2) == 1,
            };
        }

        private static float Score(Vector2 faceSize, Vector2 rectSize, float tpu) {
            // Logarithmic ratios so that "twice as big" and "half as big" are penalised equally.
            var aspectError = Mathf.Abs(Mathf.Log((faceSize.x / faceSize.y) / (rectSize.x / rectSize.y)));
            var score = aspectError * 2f;

            if (tpu > 0) {
                var faceTexels = faceSize * tpu;
                var sizeError = Mathf.Abs(Mathf.Log((faceTexels.x * faceTexels.y) / (rectSize.x * rectSize.y)));
                score += sizeError * 0.5f;
            }
            return score;
        }

        private void OnValidate() {
            texelsPerUnit = Mathf.Max(0, texelsPerUnit);
        }
    }

    public static class RectExtensions {
        public static Vector2 TopLeft(this Rect rect) {
            return new Vector2(rect.xMin, rect.yMin);
        }
        public static Vector2 TopRight(this Rect rect) {
            return new Vector2(rect.xMax, rect.yMin);
        }
        public static Vector2 BottomRight(this Rect rect) {
            return new Vector2(rect.xMax, rect.yMax);
        }
        public static Vector2 BottomLeft(this Rect rect) {
            return new Vector2(rect.xMin, rect.yMax);
        }
        /// <summary>Returns the same rect with positive width and height.</summary>
        public static Rect Normalized(this Rect rect) {
            return Rect.MinMaxRect(
                Mathf.Min(rect.xMin, rect.xMax), Mathf.Min(rect.yMin, rect.yMax),
                Mathf.Max(rect.xMin, rect.xMax), Mathf.Max(rect.yMin, rect.yMax));
        }
        public static Rect ScaleSizeBy(this Rect rect, float scale, Vector2 pivotPoint) {
            Rect result = rect;
            result.x -= pivotPoint.x;
            result.y -= pivotPoint.y;
            result.xMin *= scale;
            result.xMax *= scale;
            result.yMin *= scale;
            result.yMax *= scale;
            result.x += pivotPoint.x;
            result.y += pivotPoint.y;
            return result;
        }
    }
}
