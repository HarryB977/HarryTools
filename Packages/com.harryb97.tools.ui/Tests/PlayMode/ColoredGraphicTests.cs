using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HarryTools.UI.Tests
{
    public class ColoredGraphicTests
    {
        private GameObject go;
        private ColorPalette palette;

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            if (go) Object.DestroyImmediate(go);
            if (palette) Object.DestroyImmediate(palette);
        }

        private (Image image, ColoredGraphic colored) Create(bool withProvider)
        {
            go = new GameObject("Root", typeof(RectTransform));
            if (withProvider)
            {
                palette = ScriptableObject.CreateInstance<ColorPalette>();
                palette.SetColor(ColorType.Accent, Color.red);
                go.AddComponent<PaletteProvider>().palette = palette;
            }

            var child = new GameObject("Image", typeof(RectTransform));
            child.transform.SetParent(go.transform, false);
            var image = child.AddComponent<Image>();
            var colored = child.AddComponent<ColoredGraphic>();
            colored.SetColor(new ColorInfo { enabled = true, color = ColorType.Accent });
            colored.Refresh();
            return (image, colored);
        }

        [Test]
        public void NoProvider_UsesDefaultPalette()
        {
            var (image, _) = Create(false);
            Assert.That(image.color, Is.EqualTo(ColorPalette.Default.GetColor(ColorType.Accent)));
        }

        [UnityTest]
        public IEnumerator PaletteChange_BlendsOverDuration_IgnoringTimeScale()
        {
            var (image, colored) = Create(true);
            colored.transitionDuration = 0.2f;
            Assert.That(image.color, Is.EqualTo(Color.red));
            Time.timeScale = 0f;

            palette.SetColor(ColorType.Accent, Color.blue);
            yield return null;
            yield return null;

            Assert.That(image.color, Is.Not.EqualTo(Color.red));
            Assert.That(image.color, Is.Not.EqualTo(Color.blue));

            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(image.color, Is.EqualTo(Color.blue));
        }
    }
}
