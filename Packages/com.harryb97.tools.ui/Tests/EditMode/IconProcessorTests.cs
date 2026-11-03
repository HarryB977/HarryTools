using System.Linq;
using NUnit.Framework;
using HarryTools.UI;
using TMPro;
using UnityEngine;

namespace HarryTools.UI.Tests
{
    public class IconProcessorTests
    {
        private GameObject _go;
        private IconProcessor _processor;
        private TextMeshProUGUI _text;
        private string _iconName;
        private string _iconGlyph;

        private string expectedReplacement => $"<font=\"MaterialIcons\">{_iconGlyph}</font>";

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("IconProcessorTest", typeof(RectTransform));
            _text = _go.AddComponent<TextMeshProUGUI>();
            _text.richText = true;
            _processor = _go.AddComponent<IconProcessor>();
            _processor.textComponent = _text;

            var first = MaterialIcons.ICONS_MAP.First();
            _iconName = first.Key;
            _iconGlyph = first.Value;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void ReplacesSimpleTag()
        {
            var result = _processor.PreprocessText($"a <icon={_iconName}> b");
            Assert.AreEqual($"a {expectedReplacement} b", result);
        }

        [Test]
        public void ReplacesQuotedTag()
        {
            var result = _processor.PreprocessText($"<icon=\"{_iconName}\">");
            Assert.AreEqual(expectedReplacement, result);
        }

        [Test]
        public void ReplacesSelfClosingTag()
        {
            var result = _processor.PreprocessText($"<icon={_iconName}/>");
            Assert.AreEqual(expectedReplacement, result);
        }

        [Test]
        public void ReplacesMultipleTags()
        {
            var result = _processor.PreprocessText($"<icon={_iconName}> and <icon={_iconName}>");
            Assert.AreEqual($"{expectedReplacement} and {expectedReplacement}", result);
        }

        [Test]
        public void LeavesUnknownIconUntouched()
        {
            const string input = "<icon=definitely_not_a_real_icon_xyz>";
            Assert.AreEqual(input, _processor.PreprocessText(input));
        }

        [Test]
        public void LeavesUnterminatedTagUntouched()
        {
            var input = $"<icon={_iconName}";
            Assert.AreEqual(input, _processor.PreprocessText(input));
        }

        [Test]
        public void LeavesPlainTextUntouched()
        {
            const string input = "hello world, no icons here";
            Assert.AreEqual(input, _processor.PreprocessText(input));
        }

        [Test]
        public void EmptyString_ReturnsUnchanged()
        {
            Assert.AreEqual("", _processor.PreprocessText(""));
        }

        [Test]
        public void NullString_ReturnsUnchanged()
        {
            Assert.IsNull(_processor.PreprocessText(null));
        }

        [Test]
        public void RichTextDisabled_ReturnsUnchanged()
        {
            _text.richText = false;
            var input = $"<icon={_iconName}>";
            Assert.AreEqual(input, _processor.PreprocessText(input));
        }
    }
}
