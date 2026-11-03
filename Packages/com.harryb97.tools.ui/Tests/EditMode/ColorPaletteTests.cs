using System;
using NUnit.Framework;
using HarryTools.UI;
using UnityEngine;

namespace HarryTools.UI.Tests
{
    public class ColorPaletteTests
    {
        private ColorPalette _palette;

        [SetUp]
        public void SetUp()
        {
            _palette = ScriptableObject.CreateInstance<ColorPalette>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_palette);
        }

        [Test]
        public void SetColor_GetColor_RoundTripsForAllTypes()
        {
            var types = (ColorType[])Enum.GetValues(typeof(ColorType));

            for (int i = 0; i < types.Length; i++)
            {
                var expected = new Color(i * 0.05f, 0.25f, 0.5f, 1f);
                _palette.SetColor(types[i], expected);
                Assert.AreEqual(expected, _palette.GetColor(types[i]), $"Round trip failed for {types[i]}");
            }
        }

        [Test]
        public void SetContrast_GetContrast_RoundTrips()
        {
            var contrastTypes = new[]
            {
                ColorType.Background, ColorType.Surface, ColorType.Accent,
                ColorType.Success, ColorType.Warning, ColorType.Danger
            };

            for (int i = 0; i < contrastTypes.Length; i++)
            {
                var expected = new Color(0.1f, i * 0.05f, 0.75f, 1f);
                _palette.SetContrast(contrastTypes[i], expected);
                Assert.AreEqual(expected, _palette.GetContrast(contrastTypes[i]), $"Round trip failed for {contrastTypes[i]}");
            }
        }

        [Test]
        public void GetContrast_BaseColors_MapToOpposites()
        {
            _palette.SetColor(ColorType.Black, Color.magenta);
            _palette.SetColor(ColorType.White, Color.cyan);

            Assert.AreEqual(Color.cyan, _palette.GetContrast(ColorType.Black));
            Assert.AreEqual(Color.magenta, _palette.GetContrast(ColorType.White));
            Assert.AreEqual(Color.cyan, _palette.GetContrast(ColorType.Muted));
        }

        [Test]
        public void SetContrast_BaseColors_Throws()
        {
            Assert.Throws<ArgumentException>(() => _palette.SetContrast(ColorType.Black, Color.red));
            Assert.Throws<ArgumentException>(() => _palette.SetContrast(ColorType.White, Color.red));
            Assert.Throws<ArgumentException>(() => _palette.SetContrast(ColorType.Muted, Color.red));
        }

        [Test]
        public void SetColor_RaisesOnChange()
        {
            int calls = 0;
            _palette.onChange += () => calls++;

            _palette.SetColor(ColorType.Accent, Color.red);
            Assert.AreEqual(1, calls);

            _palette.SetContrast(ColorType.Accent, Color.blue);
            Assert.AreEqual(2, calls);
        }
    }

    public class ColorInfoTests
    {
        [Test]
        public void GetColor_NullPalette_ReturnsWhite()
        {
            var info = new ColorInfo { enabled = true, color = ColorType.Danger };
            Assert.AreEqual(Color.white, info.GetColor(null));
        }

        [Test]
        public void GetColor_UsesPaletteColorOrContrast()
        {
            var palette = ScriptableObject.CreateInstance<ColorPalette>();
            palette.SetColor(ColorType.Accent, Color.red);
            palette.SetContrast(ColorType.Accent, Color.green);

            var plain = new ColorInfo { enabled = true, color = ColorType.Accent };
            var contrast = new ColorInfo { enabled = true, color = ColorType.Accent, contrast = true };

            Assert.AreEqual(Color.red, plain.GetColor(palette));
            Assert.AreEqual(Color.green, contrast.GetColor(palette));

            UnityEngine.Object.DestroyImmediate(palette);
        }
    }
}
