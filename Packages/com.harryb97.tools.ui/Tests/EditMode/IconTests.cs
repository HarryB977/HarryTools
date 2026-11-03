using System.Collections.Generic;
using System.Linq;
using HarryTools.UI.Editor;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace HarryTools.UI.Tests
{
    public class IconTests
    {
        private readonly List<Object> owned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in owned) if (o) Object.DestroyImmediate(o);
            owned.Clear();
        }

        private IconSet CreateSet(string id, params IconGlyph[] glyphs)
        {
            var set = ScriptableObject.CreateInstance<IconSet>();
            set.SetData(id, null, null, glyphs.ToList());
            owned.Add(set);
            return set;
        }

        private IconLibrary CreateLibrary(IconSet defaultSet, params IconSet[] others)
        {
            var library = ScriptableObject.CreateInstance<IconLibrary>();
            library.SetSets(defaultSet, new[] { defaultSet }.Concat(others).ToList());
            owned.Add(library);
            return library;
        }

        private static IconGlyph Glyph(string name, uint codepoint, string[] aliases = null, string[] tags = null) =>
            new IconGlyph { name = name, codepoint = codepoint, aliases = aliases ?? new string[0], tags = tags ?? new string[0] };

        [Test]
        public void ParseMdiMeta_ReadsNameCodepointAliasesTags()
        {
            const string json = "[{\"id\":\"x\",\"name\":\"ab-testing\",\"codepoint\":\"F01C9\",\"aliases\":[],\"tags\":[\"Developer / Languages\"]}," +
                                "{\"name\":\"home\",\"codepoint\":\"F02DC\",\"aliases\":[\"house\"],\"tags\":[\"Places\"]}]";

            var glyphs = IconSetImporter.ParseMdiMeta(json);

            Assert.That(glyphs.Count, Is.EqualTo(2));
            Assert.That(glyphs[0].name, Is.EqualTo("ab-testing"));
            Assert.That(glyphs[0].codepoint, Is.EqualTo(0xF01C9u));
            Assert.That(glyphs[0].tags, Is.EqualTo(new[] { "Developer / Languages" }));
            Assert.That(glyphs[1].aliases, Is.EqualTo(new[] { "house" }));
        }

        [Test]
        public void ParseCodepoints_ReadsLines_IgnoresBlank()
        {
            var glyphs = IconSetImporter.ParseCodepoints("home e88a\n\n search e8b6 \r\n");

            Assert.That(glyphs.Select(g => g.name), Is.EqualTo(new[] { "home", "search" }));
            Assert.That(glyphs[1].codepoint, Is.EqualTo(0xE8B6u));
        }

        [Test]
        public void TryGetGlyph_MatchesAlias_CaseInsensitive_DashUnderscoreEquivalent()
        {
            var set = CreateSet("mdi", Glyph("account-alert", 0xF0005, new[] { "user-alert" }));

            Assert.That(set.TryGetGlyph("account_alert", out var a), Is.True);
            Assert.That(a.codepoint, Is.EqualTo(0xF0005u));
            Assert.That(set.TryGetGlyph("ACCOUNT-ALERT", out _), Is.True);
            Assert.That(set.TryGetGlyph("user_alert", out _), Is.True);
            Assert.That(set.TryGetGlyph("nope", out _), Is.False);
        }

        [Test]
        public void Library_Resolve_DefaultAndPrefixed()
        {
            var mdi = CreateSet("mdi", Glyph("home", 0xF02DC));
            var tabler = CreateSet("tabler", Glyph("home", 0xEAC1));
            var library = CreateLibrary(mdi, tabler);

            Assert.That(library.TryResolve("home", out var s1, out var g1), Is.True);
            Assert.That(s1, Is.SameAs(mdi));
            Assert.That(g1.codepoint, Is.EqualTo(0xF02DCu));

            Assert.That(library.TryResolve("tabler:home", out var s2, out var g2), Is.True);
            Assert.That(s2, Is.SameAs(tabler));
            Assert.That(g2.codepoint, Is.EqualTo(0xEAC1u));

            Assert.That(library.TryResolve("missing:home", out _, out _), Is.False);
        }

        [Test]
        public void Search_Ranking_ExactBeforePrefixBeforeAliasBeforeTag()
        {
            var set = CreateSet("mdi",
                Glyph("tag-home", 1, tags: new[] { "home" }),
                Glyph("house-x", 2, aliases: new[] { "home-alt" }),
                Glyph("home-variant", 3),
                Glyph("my-home-outline", 4),
                Glyph("home", 5));

            var names = IconSearch.Find(new[] { set }, "home").Select(r => r.glyph.name).ToList();

            Assert.That(names, Is.EqualTo(new[] { "home", "home-variant", "house-x", "tag-home", "my-home-outline" }));
        }

        [Test]
        public void Search_FilterBySet()
        {
            var a = CreateSet("a", Glyph("star", 1));
            var b = CreateSet("b", Glyph("star", 2));

            var results = IconSearch.Find(new[] { a, b }, "star", "b");

            Assert.That(results.Count, Is.EqualTo(1));
            Assert.That(results[0].set, Is.SameAs(b));
        }

        [Test]
        public void DefaultLibrary_HasMdiWith7000PlusGlyphs()
        {
            var library = IconLibrary.Instance;

            Assert.That(library, Is.Not.Null);
            Assert.That(library.DefaultSet, Is.Not.Null);
            Assert.That(library.DefaultSet.Id, Is.EqualTo("mdi"));
            Assert.That(library.DefaultSet.Glyphs.Count, Is.GreaterThan(7000));
            Assert.That(library.DefaultSet.FontAsset, Is.Not.Null);
            Assert.That(library.TryResolve("home", out _, out var home), Is.True);
            Assert.That(home.codepoint, Is.EqualTo(0xF02DCu));
        }
    }

    public class IconProcessorTests
    {
        private GameObject go;
        private IconProcessor processor;
        private TextMeshProUGUI text;
        private IconSet set;
        private IconLibrary library;
        private TMP_FontAsset font;

        private string Expected => $"<font=\"{font.name}\">{char.ConvertFromUtf32(0xF02DC)}</font>";

        [SetUp]
        public void SetUp()
        {
            font = ScriptableObject.CreateInstance<TMP_FontAsset>();
            font.name = "TestIcons";
            set = ScriptableObject.CreateInstance<IconSet>();
            set.SetData("mdi", font, null, new List<IconGlyph> { new IconGlyph { name = "home", codepoint = 0xF02DC, aliases = new string[0], tags = new string[0] } });
            library = ScriptableObject.CreateInstance<IconLibrary>();
            library.SetSets(set, new List<IconSet> { set });

            go = new GameObject("IconProcessorTest", typeof(RectTransform));
            text = go.AddComponent<TextMeshProUGUI>();
            text.richText = true;
            processor = go.AddComponent<IconProcessor>();
            processor.Library = library;
            processor.textComponent = text;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(library);
            Object.DestroyImmediate(set);
            Object.DestroyImmediate(font);
        }

        [Test] public void ReplacesSimpleTag() => Assert.AreEqual($"a {Expected} b", processor.PreprocessText("a <icon=home> b"));
        [Test] public void ReplacesPrefixedTag() => Assert.AreEqual(Expected, processor.PreprocessText("<icon=mdi:home>"));
        [Test] public void ReplacesQuotedTag() => Assert.AreEqual(Expected, processor.PreprocessText("<icon=\"home\">"));
        [Test] public void ReplacesSelfClosingTag() => Assert.AreEqual(Expected, processor.PreprocessText("<icon=home/>"));
        [Test] public void ReplacesMultipleTags() => Assert.AreEqual($"{Expected} and {Expected}", processor.PreprocessText("<icon=home> and <icon=home>"));
        [Test] public void LeavesUnknownIconUntouched() => Assert.AreEqual("<icon=not_real_xyz>", processor.PreprocessText("<icon=not_real_xyz>"));
        [Test] public void LeavesUnterminatedTagUntouched() => Assert.AreEqual("<icon=home", processor.PreprocessText("<icon=home"));
        [Test] public void LeavesPlainTextUntouched() => Assert.AreEqual("hello", processor.PreprocessText("hello"));
        [Test] public void EmptyString_ReturnsUnchanged() => Assert.AreEqual("", processor.PreprocessText(""));
        [Test] public void NullString_ReturnsUnchanged() => Assert.IsNull(processor.PreprocessText(null));

        [Test]
        public void RichTextDisabled_ReturnsUnchanged()
        {
            text.richText = false;
            Assert.AreEqual("<icon=home>", processor.PreprocessText("<icon=home>"));
        }
    }
}
