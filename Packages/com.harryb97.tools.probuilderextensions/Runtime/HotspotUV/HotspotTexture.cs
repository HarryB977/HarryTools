using System.Collections.Generic;
using UnityEngine;

namespace HarryTools.ProBuilderExtensions.HotspotUV {
    [CreateAssetMenu(fileName = "New Hotspot Texture", menuName = "HarryTools/ProBuilder Extensions/Hotspot Texture", order = 300)]
    public class HotspotTexture : ScriptableObject {
        public Texture target;

        [Tooltip("Texels per world unit. Used to pick the hotspot whose size best matches the face. Set to 0 to match only by aspect ratio.")]
        public float texelsPerUnit = 256f;

        // Rects are stored in texture pixels, with the origin at the top-left corner of the texture.
        public List<Rect> rects = new List<Rect>();

        public bool IsValid => target != null && rects != null && rects.Count > 0;

        public Vector2 TextureSize => target != null ? new Vector2(target.width, target.height) : Vector2.one;

        /// <summary>
        /// Returns the rect at <paramref name="index"/> in UV space (0-1, origin at the bottom-left).
        /// </summary>
        public Rect GetUVRect(int index) {
            var size = TextureSize;
            var r = rects[index].Normalized();
            return Rect.MinMaxRect(r.xMin / size.x, 1 - r.yMax / size.y, r.xMax / size.x, 1 - r.yMin / size.y);
        }

        /// <summary>
        /// Picks the hotspot that best matches a face of <paramref name="faceSize"/> (world units),
        /// prioritising aspect ratio and then size. Among rects that score within
        /// <paramref name="randomTolerance"/> of the best one, a random one is chosen.
        /// </summary>
        /// <returns>The rect index, or -1 if there is no valid rect.</returns>
        public int FindBestRect(Vector2 faceSize, bool allowRotation, float randomTolerance, out bool rotated) {
            rotated = false;
            if (!IsValid || faceSize.x <= Mathf.Epsilon || faceSize.y <= Mathf.Epsilon)
                return -1;

            var scores = new List<(int index, bool rotated, float score)>();
            var best = float.MaxValue;

            for (int i = 0; i < rects.Count; i++) {
                var r = rects[i].Normalized();
                if (r.width <= 0 || r.height <= 0) continue;

                for (int o = 0; o < (allowRotation ? 2 : 1); o++) {
                    var rectSize = o == 0 ? r.size : new Vector2(r.height, r.width);
                    var score = Score(faceSize, rectSize);
                    scores.Add((i, o == 1, score));
                    best = Mathf.Min(best, score);
                }
            }

            if (scores.Count == 0) return -1;

            var candidates = scores.FindAll(s => s.score <= best + randomTolerance);
            var pick = candidates[Random.Range(0, candidates.Count)];
            rotated = pick.rotated;
            return pick.index;
        }

        private float Score(Vector2 faceSize, Vector2 rectSize) {
            // Logarithmic ratios so that "twice as big" and "half as big" are penalised equally.
            var aspectError = Mathf.Abs(Mathf.Log((faceSize.x / faceSize.y) / (rectSize.x / rectSize.y)));
            var score = aspectError * 2f;

            if (texelsPerUnit > 0) {
                var faceTexels = faceSize * texelsPerUnit;
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
