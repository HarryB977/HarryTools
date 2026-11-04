using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace HarryTools.ProBuilderExtensions.Generator {
    /// <summary>Turns a grunge-frame texture into the border strip the generator wraps along every patch edge.</summary>
    public static class HotspotWearMask {
        /// <summary>
        /// Reads the left <paramref name="border"/> fraction of <paramref name="source"/> (column 0 = the rim) and keeps
        /// only the dark pixels connected to the rim, so no loose specks float inside the patches. The rows at both ends
        /// (as many as the strip is wide, at most a quarter each) are skipped: they hold the frame's corners, where its
        /// top and bottom bands would cross the whole strip. A strip wider than <paramref name="maxWidth"/> is first
        /// averaged down to it (the band's size in output pixels), so detail finer than a pixel cannot break apart into
        /// loose dots when the generator samples it.
        /// </summary>
        /// <returns>A new readable R8 texture (1 = worn) that the caller owns and must destroy.</returns>
        public static Texture2D Build(Texture2D source, float border, int maxWidth = int.MaxValue) {
            int width = Mathf.Clamp(Mathf.RoundToInt(border * source.width), 1, source.width);
            int skip = Mathf.Min(width, source.height / 4);
            int height = source.height - 2 * skip;
            var luminance = ReadLuminance(source, width, skip, height);
            if (width > maxWidth) luminance = Downsample(luminance, ref width, ref height, Mathf.Max(1, maxWidth));

            const float dark = 0.5f;
            var keep = new bool[width * height];
            var queue = new Queue<int>();
            for (int y = 0; y < height; y++) {
                int i = y * width;
                if (luminance[i] < dark) { keep[i] = true; queue.Enqueue(i); }
            }
            while (queue.Count > 0) {
                int i = queue.Dequeue();
                int x = i % width, y = i / width;
                Visit(x - 1, y); Visit(x + 1, y); Visit(x, y - 1); Visit(x, y + 1);
            }

            void Visit(int x, int y) {
                if (x < 0 || x >= width || y < 0 || y >= height) return;
                int i = y * width + x;
                if (keep[i] || luminance[i] >= dark) return;
                keep[i] = true;
                queue.Enqueue(i);
            }

            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++) {
                byte v = keep[i] ? (byte)Mathf.RoundToInt((1 - luminance[i]) * 255) : (byte)0;
                pixels[i] = new Color32(v, v, v, 255);
            }
            var mask = new Texture2D(width, height, TextureFormat.R8, false, true) {
                wrapModeU = TextureWrapMode.Clamp,
                wrapModeV = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
                name = source.name + " (wear mask)",
            };
            mask.SetPixels32(pixels);
            mask.Apply(false, false);
            return mask;
        }

        /// <summary>Box-filters a width × height grid down to <paramref name="target"/> columns, keeping its proportions.</summary>
        private static float[] Downsample(float[] values, ref int width, ref int height, int target) {
            int newHeight = Mathf.Max(1, Mathf.RoundToInt((float)height * target / width));
            var result = new float[target * newHeight];
            for (int y = 0; y < newHeight; y++) {
                int y0 = y * height / newHeight, y1 = Mathf.Max(y0 + 1, (y + 1) * height / newHeight);
                for (int x = 0; x < target; x++) {
                    int x0 = x * width / target, x1 = Mathf.Max(x0 + 1, (x + 1) * width / target);
                    float sum = 0;
                    for (int sy = y0; sy < y1; sy++)
                        for (int sx = x0; sx < x1; sx++) sum += values[sy * width + sx];
                    result[y * target + x] = sum / ((x1 - x0) * (y1 - y0));
                }
            }
            width = target;
            height = newHeight;
            return result;
        }

        /// <summary>
        /// Perceptual luminance of a block at the left of the texture, read on the GPU so non-readable textures work.
        /// Single-channel textures (R8, BC4…) use their red channel and Alpha8 its alpha.
        /// </summary>
        private static float[] ReadLuminance(Texture2D source, int width, int y, int height) {
            var previous = RenderTexture.active;
            var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var copy = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            try {
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;
                copy.ReadPixels(new Rect(0, y, width, height), 0, 0, false);
                copy.Apply(false, false);
                var pixels = copy.GetPixels32();
                var luminance = new float[pixels.Length];
                bool alphaOnly = source.format == TextureFormat.Alpha8;
                bool singleChannel = !alphaOnly && GraphicsFormatUtility.GetComponentCount(source.graphicsFormat) == 1;
                for (int i = 0; i < pixels.Length; i++) {
                    var p = pixels[i];
                    luminance[i] = alphaOnly ? p.a / 255f
                        : singleChannel ? p.r / 255f
                        : (0.2126f * p.r + 0.7152f * p.g + 0.0722f * p.b) / 255f;
                }
                return luminance;
            } finally {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                Object.DestroyImmediate(copy);
            }
        }
    }
}
