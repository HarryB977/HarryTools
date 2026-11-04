using System.Collections.Generic;
using UnityEngine;

namespace HarryTools.ProBuilderExtensions.Generator {
    /// <summary>Turns a grunge-frame texture into the border strip the generator wraps along every patch edge.</summary>
    public static class HotspotWearMask {
        /// <summary>
        /// Reads the left <paramref name="border"/> fraction of <paramref name="source"/> (column 0 = the rim) and keeps
        /// only the dark pixels connected to the rim, so no loose specks float inside the patches.
        /// </summary>
        /// <returns>A new readable R8 texture (1 = worn) that the caller owns and must destroy.</returns>
        public static Texture2D Build(Texture2D source, float border) {
            int width = Mathf.Clamp(Mathf.RoundToInt(border * source.width), 1, source.width);
            int height = source.height;
            var luminance = ReadLuminance(source, width);

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

        /// <summary>Perceptual luminance of the first <paramref name="width"/> columns, read on the GPU so non-readable textures work.</summary>
        private static float[] ReadLuminance(Texture2D source, int width) {
            var previous = RenderTexture.active;
            var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var copy = new Texture2D(width, source.height, TextureFormat.RGBA32, false, false);
            try {
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;
                copy.ReadPixels(new Rect(0, 0, width, source.height), 0, 0, false);
                copy.Apply(false, false);
                var pixels = copy.GetPixels32();
                var luminance = new float[pixels.Length];
                for (int i = 0; i < pixels.Length; i++)
                    luminance[i] = (0.2126f * pixels[i].r + 0.7152f * pixels[i].g + 0.0722f * pixels[i].b) / 255f;
                return luminance;
            } finally {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                Object.DestroyImmediate(copy);
            }
        }
    }
}
