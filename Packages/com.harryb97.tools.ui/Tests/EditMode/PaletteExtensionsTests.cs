using NUnit.Framework;
using UnityEngine;

namespace HarryTools.UI.Tests
{
    public class PaletteExtensionsTests
    {
        private ColorPalette palette;

        [SetUp]
        public void SetUp() => palette = ScriptableObject.CreateInstance<ColorPalette>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(palette);

        private static float Luminance(Color c) => ColorContrast.RelativeLuminance(c);

        [Test]
        public void Contrast_BlackWhite_Is21() =>
            Assert.That(ColorContrast.Ratio(Color.black, Color.white), Is.EqualTo(21f).Within(0.01f));

        [Test]
        public void Contrast_SameColor_Is1() =>
            Assert.That(ColorContrast.Ratio(Color.gray, Color.gray), Is.EqualTo(1f).Within(0.001f));

        [Test]
        public void MeetsAA_Gray777OnWhite_FalseForNormalTrueForLarge()
        {
            ColorUtility.TryParseHtmlString("#777777", out var gray);

            Assert.That(ColorContrast.Ratio(gray, Color.white), Is.EqualTo(4.48f).Within(0.02f));
            Assert.That(ColorContrast.MeetsAA(gray, Color.white), Is.False);
            Assert.That(ColorContrast.MeetsAA(gray, Color.white, true), Is.True);
        }

        [Test]
        public void Tone_Zero_ReturnsSameColor()
        {
            var c = new Color(0.2f, 0.5f, 0.8f, 1f);
            Assert.That(ColorTones.Apply(c, 0), Is.EqualTo(c));
        }

        [Test]
        public void Tone_Positive_IsLighter_Negative_IsDarker()
        {
            var c = new Color(0.2f, 0.5f, 0.8f, 1f);
            Assert.That(Luminance(ColorTones.Apply(c, 1)), Is.GreaterThan(Luminance(c)));
            Assert.That(Luminance(ColorTones.Apply(c, 2)), Is.GreaterThan(Luminance(ColorTones.Apply(c, 1))));
            Assert.That(Luminance(ColorTones.Apply(c, -1)), Is.LessThan(Luminance(c)));
        }

        [Test]
        public void Tone_KeepsAlpha()
        {
            var c = new Color(0.2f, 0.5f, 0.8f, 0.4f);
            Assert.That(ColorTones.Apply(c, 2).a, Is.EqualTo(0.4f).Within(0.0001f));
        }

        [Test]
        public void AddCustomRole_AssignsIncreasingIds_StartingAt1001()
        {
            var gold = palette.AddCustomRole("Oro", Color.yellow, Color.black);
            var epic = palette.AddCustomRole("Épico", Color.magenta, Color.white);

            Assert.That(gold.id, Is.EqualTo(1001));
            Assert.That(epic.id, Is.EqualTo(1002));
            Assert.That(palette.CustomRoles.Count, Is.EqualTo(2));
        }

        [Test]
        public void RemovedId_IsNotReused()
        {
            var gold = palette.AddCustomRole("Oro", Color.yellow, Color.black);
            Assert.That(palette.RemoveCustomRole(gold.id), Is.True);

            var next = palette.AddCustomRole("Plata", Color.gray, Color.black);

            Assert.That(next.id, Is.EqualTo(1002));
            Assert.That(palette.TryGetCustomRole(gold.id, out _), Is.False);
        }

        [Test]
        public void ColorInfo_Custom_ResolvesByIdAfterRename()
        {
            var gold = palette.AddCustomRole("Oro", Color.yellow, Color.black);
            var info = new ColorInfo { enabled = true, color = ColorType.Custom, customRoleId = gold.id };

            gold.name = "Dorado";
            palette.AddCustomRole("Otro", Color.cyan, Color.black);

            Assert.That(info.GetColor(palette), Is.EqualTo(Color.yellow));
        }

        [Test]
        public void ColorInfo_Custom_ContrastAndTone_Apply()
        {
            var gold = palette.AddCustomRole("Oro", new Color(0.8f, 0.6f, 0.1f), Color.black);

            var contrast = new ColorInfo { enabled = true, color = ColorType.Custom, customRoleId = gold.id, contrast = true };
            var lighter = new ColorInfo { enabled = true, color = ColorType.Custom, customRoleId = gold.id, tone = 1 };

            Assert.That(contrast.GetColor(palette), Is.EqualTo(Color.black));
            Assert.That(lighter.GetColor(palette), Is.EqualTo(ColorTones.Apply(gold.color, 1)));
        }

        [Test]
        public void ColorInfo_BuiltInWithTone_Applies()
        {
            palette.SetColor(ColorType.Accent, new Color(0.1f, 0.4f, 0.9f));
            var info = new ColorInfo { enabled = true, color = ColorType.Accent, tone = -2 };

            Assert.That(info.GetColor(palette), Is.EqualTo(ColorTones.Apply(new Color(0.1f, 0.4f, 0.9f), -2)));
        }

        [Test]
        public void ColorInfo_MissingCustomRole_ReturnsMagenta()
        {
            var info = new ColorInfo { enabled = true, color = ColorType.Custom, customRoleId = 4242 };
            Assert.That(info.GetColor(palette), Is.EqualTo(Color.magenta));
        }

        [Test]
        public void DefaultPalette_LoadsFromResources()
        {
            Assert.That(ColorPalette.Default, Is.Not.Null);
            Assert.That(ColorPalette.Default.GetColor(ColorType.Accent).a, Is.GreaterThan(0f));
        }
    }
}
