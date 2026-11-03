using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HarryTools.UI.Tests
{
    /// <summary> Rectángulo 80x80 centrado (de 24 a 104) en un render de 128x128 </summary>
    public class ProceduralRenderTests
    {
        private UIRenderTestUtility render;

        [TearDown]
        public void TearDown() => render?.Dispose();

        private RectangleGraphic CreateRectangle(Color background, Transform parent = null)
        {
            render = new UIRenderTestUtility(128, 128, background);
            var rect = render.CreateRect("Rectangle", parent, 24, 24, 80, 80);
            var graphic = rect.gameObject.AddComponent<RectangleGraphic>();
            graphic.useMaxRoundness = false;
            graphic.uniformRoundness = true;
            graphic.roundnessInPixels = Vector4.zero;
            graphic.graphicColor = Color.white;
            return graphic;
        }

        [UnityTest]
        public IEnumerator Defaults_RenderSameAsBefore()
        {
            var graphic = CreateRectangle(Color.black);
            var fill = new Color(0.2f, 0.6f, 0.4f, 1f);
            graphic.graphicColor = fill;
            yield return null;

            var pixel = render.Render().GetPixel(64, 64);

            Assert.That(pixel.r, Is.EqualTo(fill.r).Within(2f / 255f));
            Assert.That(pixel.g, Is.EqualTo(fill.g).Within(2f / 255f));
            Assert.That(pixel.b, Is.EqualTo(fill.b).Within(2f / 255f));
            Assert.That(graphic.materialForRendering.IsKeywordEnabled("HT_EXTENDED"), Is.False);
        }

        [UnityTest]
        public IEnumerator InnerShadow_DarkensNearEdgeMoreThanCenter()
        {
            var graphic = CreateRectangle(Color.gray);
            graphic.innerShadowColor = Color.black;
            graphic.innerShadowSize = 10f;
            yield return null;

            var texture = render.Render();
            float edge = UIRenderTestUtility.Luminance(texture.GetPixel(27, 64));
            float center = UIRenderTestUtility.Luminance(texture.GetPixel(64, 64));

            Assert.That(edge, Is.LessThan(center * 0.8f));
        }

        [UnityTest]
        public IEnumerator OutlineGradient_LeftAndRightUseGradientEnds()
        {
            var graphic = CreateRectangle(Color.black);
            graphic.outlineSize = 6f;
            graphic.outlineColor = Color.red;
            graphic.outlineGradientType = OutlineGradientType.Horizontal;
            graphic.outlineGradientColor = Color.blue;
            yield return null;

            var texture = render.Render();
            var left = texture.GetPixel(21, 64);
            var right = texture.GetPixel(106, 64);

            Assert.That(left.r, Is.GreaterThan(left.b), "left " + left);
            Assert.That(right.b, Is.GreaterThan(right.r), "right " + right);
        }

        [UnityTest]
        public IEnumerator ShadowOffset_ShiftsShadow()
        {
            var graphic = CreateRectangle(Color.white);
            graphic.shadowColor = Color.black;
            graphic.shadowSize = 6f;
            graphic.shadowBlur = 0f;
            graphic.shadowOffset = new Vector2(10f, -10f);
            yield return null;

            var texture = render.Render();
            float bottomRight = UIRenderTestUtility.Luminance(texture.GetPixel(110, 20));
            float topLeft = UIRenderTestUtility.Luminance(texture.GetPixel(18, 110));

            Assert.That(bottomRight, Is.LessThan(0.5f));
            Assert.That(topLeft, Is.GreaterThan(0.95f));
        }

        [UnityTest]
        public IEnumerator ShadowOffset_AnimatedShadowSizeFromZero_StillOffsets()
        {
            var graphic = CreateRectangle(Color.white);
            graphic.shadowColor = Color.black;
            graphic.shadowBlur = 0f;
            graphic.shadowOffset = new Vector2(10f, -10f);
            graphic.shadowSize = 0f;
            yield return null;
            render.Render();

            graphic.shadowSize = 6f;
            yield return null;

            var texture = render.Render();
            Assert.That(UIRenderTestUtility.Luminance(texture.GetPixel(110, 20)), Is.LessThan(0.5f));
            Assert.That(UIRenderTestUtility.Luminance(texture.GetPixel(18, 110)), Is.GreaterThan(0.95f));
        }

        [UnityTest]
        public IEnumerator RectMask2DSoftness_ProducesPartialAlpha()
        {
            render = new UIRenderTestUtility(128, 128, Color.black);
            var maskRect = render.CreateRect("Mask", null, 0, 0, 64, 128);
            var mask = maskRect.gameObject.AddComponent<RectMask2D>();
            mask.softness = new Vector2Int(20, 20);

            var rect = render.CreateRect("Rectangle", maskRect, 24, 24, 80, 80);
            var graphic = rect.gameObject.AddComponent<RectangleGraphic>();
            graphic.useMaxRoundness = false;
            graphic.uniformRoundness = true;
            graphic.roundnessInPixels = Vector4.zero;
            graphic.graphicColor = Color.white;
            yield return null;
            yield return null;

            var texture = render.Render();
            float inBand = texture.GetPixel(58, 64).r;
            float clipped = texture.GetPixel(80, 64).r;

            Assert.That(inBand, Is.GreaterThan(0.05f).And.LessThan(0.95f), "softness band");
            Assert.That(clipped, Is.LessThan(0.05f), "outside mask");
        }
    }
}
