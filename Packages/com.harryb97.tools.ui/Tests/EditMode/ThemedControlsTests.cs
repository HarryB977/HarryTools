using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace HarryTools.UI.Tests
{
    public class ThemedControlsTests
    {
        private GameObject _parent;
        private ColorPalette _palette;
        private ButtonElement _button;
        private RectangleGraphic _graphic;

        [TearDown]
        public void TearDown()
        {
            if (_parent) Object.DestroyImmediate(_parent);
            if (_palette) Object.DestroyImmediate(_palette);
        }

        [Test]
        public void PaletteChangeWhileHovered_UpdatesBothResolvedColorsAndVisibleHover()
        {
            CreateButton();
            _button.SetColors(Info(ColorType.Accent), Info(ColorType.Warning), default(ColorTone), default(ColorTone));
            Step();
            AssertColor(_graphic.graphicColor, _palette.GetColor(ColorType.Accent));
            SimulateState(true, false);
            Step();
            AssertColor(_graphic.graphicColor, _palette.GetColor(ColorType.Warning));

            var normal = new Color(.13f, .65f, .31f, 1f);
            var hover = new Color(.82f, .24f, .46f, 1f);
            _palette.SetColor(ColorType.Accent, normal);
            _palette.SetColor(ColorType.Warning, hover);
            // No manual palette refresh: these changes must reach the button
            // through PaletteProvider notifications while it remains hovered.
            Step();
            AssertColor(_button.backgroundNormal, normal);
            AssertColor(_button.backgroundHover, hover);
            AssertColor(_graphic.graphicColor, hover);
            SimulateState(false, false);
            Step();
            AssertColor(_graphic.graphicColor, normal);
        }

        [Test]
        public void SetColors_ResolvesExplicitTonesOnTheNativeButton()
        {
            CreateButton();
            var normal = Info(ColorType.Accent);
            var hover = Info(ColorType.Warning);
            var shade = Info(ColorType.Surface, true);
            _button.SetColors(normal, hover,
                new ColorTone { shade = shade, blend = .25f },
                new ColorTone { shade = shade, blend = .6f });
            Step();
            var expectedNormal = Color.Lerp(normal.GetColor(_palette), shade.GetColor(_palette), .25f);
            var expectedHover = Color.Lerp(hover.GetColor(_palette), shade.GetColor(_palette), .6f);
            AssertColor(_button.backgroundNormal, expectedNormal);
            AssertColor(_button.backgroundHover, expectedHover);
            AssertColor(_graphic.graphicColor, expectedNormal);
            SimulateState(true, false);
            Step();
            AssertColor(_graphic.graphicColor, expectedHover);
        }

        [Test]
        public void EditorPointerCallbacks_DoNotSimulateGameplayInteraction()
        {
            CreateButton();
            _button.SetColors(Info(ColorType.Accent), Info(ColorType.Warning), default(ColorTone), default(ColorTone));
            var authoredScale = new Vector3(.72f, .91f, 1.3f);
            _button.transform.localScale = authoredScale;
            var clicks = 0;
            _button.onClick += () => clicks++;
            _button.OnPointerEnter(null);
            _button.OnPointerDown(null);
            _button.OnPointerClick(null);
            Invoke(_button, "Update");
            Assert.That(clicks, Is.Zero);
            AssertColor(_graphic.graphicColor, _button.backgroundNormal);
            Assert.That(_button.transform.localScale, Is.EqualTo(authoredScale));
        }

        [Test]
        public void PressReleaseAndDisabledState_PreserveScaleAndDisabledOpacity()
        {
            CreateButton();
            _button.SetColors(Info(ColorType.Accent), Info(ColorType.Warning), default(ColorTone), default(ColorTone));
            SimulateState(false, true);
            Step();
            Assert.That(_button.transform.localScale, Is.EqualTo(new Vector3(.85f, .85f, 1f)));
            AssertColor(_graphic.graphicColor, _button.backgroundHover);
            SimulateState(false, false);
            Step();
            Assert.That(_button.transform.localScale, Is.EqualTo(Vector3.one));
            AssertColor(_graphic.graphicColor, _button.backgroundNormal);
            _button.interactable = false;
            SimulateState(false, true);
            Step();
            Assert.That(_button.transform.localScale, Is.EqualTo(Vector3.one));
            var disabled = _button.backgroundNormal;
            disabled.a *= .4f;
            AssertColor(_graphic.graphicColor, disabled);
        }

        [Test]
        public void ZeroDuration_AllInteractionStatesHaveFiniteColorsAndScale()
        {
            CreateButton();
            foreach (var action in new Action[]
            {
                () => { }, () => SimulateState(true, false), () => SimulateState(true, true),
                () => SimulateState(true, false), () => SimulateState(false, false),
                () => _button.interactable = false, () => _button.interactable = true
            })
            {
                action();
                Step();
                var color = _graphic.graphicColor;
                var scale = _button.transform.localScale;
                foreach (var value in new[] { color.r, color.g, color.b, color.a, scale.x, scale.y, scale.z })
                    Assert.That(float.IsNaN(value) || float.IsInfinity(value), Is.False,
                        "A zero-duration transition produced NaN/Infinity.");
            }
        }

        private void CreateButton()
        {
            _palette = ScriptableObject.CreateInstance<ColorPalette>();
            _palette.SetColor(ColorType.Accent, new Color(.1f, .3f, .7f, 1f));
            _palette.SetColor(ColorType.Warning, new Color(.7f, .4f, .1f, 1f));
            _palette.SetColor(ColorType.Surface, new Color(.1f, .12f, .14f, 1f));
            _palette.SetContrast(ColorType.Surface, Color.white);
            _parent = new GameObject("Native HeroUI button test", typeof(RectTransform));
            _parent.SetActive(false);
            _parent.AddComponent<PaletteProvider>().palette = _palette;
            var child = new GameObject("Button", typeof(RectTransform), typeof(CanvasRenderer));
            child.transform.SetParent(_parent.transform, false);
            _graphic = child.AddComponent<RectangleGraphic>();
            _button = child.AddComponent<ButtonElement>();
            Assert.That(_graphic, Is.Not.Null);
            var data = new SerializedObject(_button);
            data.FindProperty("_graphic").objectReferenceValue = _graphic;
            data.FindProperty("_transitionDuration").floatValue = 0f;
            data.FindProperty("_scaleNormal").floatValue = 1f;
            data.FindProperty("_scaleClicked").floatValue = .85f;
            data.FindProperty("_disabledAlpha").floatValue = .4f;
            data.ApplyModifiedPropertiesWithoutUndo();
            _parent.SetActive(true);
        }

        private static ColorInfo Info(ColorType type, bool contrast = false) => new ColorInfo { enabled = true, color = type, contrast = contrast };
        // UpdateVisuals is the same calculation called by the runtime Update.
        // Keeping the public pointer guards intact avoids editing prefab scale
        // merely because an ExecuteAlways component is present in the editor.
        private void Step() => Invoke(_button, "UpdateVisuals", 0f);
        private void SimulateState(bool hover, bool press)
        {
            typeof(ButtonElement).GetField("_isHovering", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_button, hover);
            typeof(ButtonElement).GetField("_isPressing", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_button, press);
        }
        private static void Invoke(object target, string name, params object[] arguments)
        {
            for (var type = target.GetType(); type != null; type = type.BaseType)
            {
                var method = type.GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (method == null) continue;
                method.Invoke(target, arguments);
                return;
            }
            Assert.Fail(target.GetType().Name + ": lifecycle method " + name + " not found");
        }
        private static void AssertColor(Color actual, Color expected)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(.001f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(.001f));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(.001f));
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(.001f));
        }
    }
}
