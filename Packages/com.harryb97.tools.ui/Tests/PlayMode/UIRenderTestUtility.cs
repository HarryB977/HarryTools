using System;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HarryTools.UI.Tests
{
    /// <summary> Renderiza un Canvas fuera de pantalla para comprobar píxeles en los tests </summary>
    public sealed class UIRenderTestUtility : IDisposable
    {
        public readonly Canvas canvas;
        private readonly Camera camera;
        private readonly RenderTexture target;
        private readonly int width, height;

        public UIRenderTestUtility(int _width, int _height, Color _background)
        {
            width = _width;
            height = _height;
            target = new RenderTexture(_width, _height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);

            camera = new GameObject("RenderTestCamera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = _background;
            camera.orthographic = true;
            camera.targetTexture = target;
            camera.enabled = false;

            canvas = new GameObject("RenderTestCanvas", typeof(RectTransform)).AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 5f;
        }

        /// <summary> RectTransform hijo del canvas, en píxeles desde la esquina inferior izquierda </summary>
        public RectTransform CreateRect(string _name, Transform _parent, float _x, float _y, float _w, float _h)
        {
            var rect = new GameObject(_name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(_parent ? _parent : canvas.transform, false);
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = Vector2.zero;
            var parentRect = _parent as RectTransform;
            Vector2 parentOrigin = parentRect && parentRect != canvas.transform ? parentRect.anchoredPosition : Vector2.zero;
            rect.anchoredPosition = new Vector2(_x, _y) - parentOrigin;
            rect.sizeDelta = new Vector2(_w, _h);
            return rect;
        }

        public Texture2D Render()
        {
            Canvas.ForceUpdateCanvases();
            camera.Render();

            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            return texture;
        }

        public static float Luminance(Color _c) => 0.2126f * _c.r + 0.7152f * _c.g + 0.0722f * _c.b;

        public void Dispose()
        {
            Object.DestroyImmediate(canvas.gameObject);
            Object.DestroyImmediate(camera.gameObject);
            target.Release();
            Object.DestroyImmediate(target);
        }
    }
}
