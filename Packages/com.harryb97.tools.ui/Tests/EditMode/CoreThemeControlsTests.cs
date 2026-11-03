using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using HarryTools.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HarryTools.UI.Tests.Core
{
    // This assembly deliberately has no reference to HarryTools.UI.Samples.HeroUI.
    public class CoreThemeControlsTests
    {
        private static readonly Type[] CoreControlTypes =
        {
            typeof(ThemeColors), typeof(ButtonElement), typeof(ToggleElement), typeof(SelectedOutlineInputField),
            typeof(ThemedToggle), typeof(ThemedInputOutline), typeof(ThemedInputFieldMandatory)
        };

        [Test]
        public void ReusableControls_BelongToCoreWithoutAHeroUIAssemblyDependency()
        {
            var core = typeof(ColoredGraphic).Assembly;
            foreach (var type in CoreControlTypes)
            {
                Assert.That(type.Assembly, Is.EqualTo(core), type.Name + " still requires the template assembly");
                Assert.That(type.Namespace, Is.EqualTo("HarryTools.UI"));
            }
            Assert.That(core.GetReferencedAssemblies().Any(reference => reference.Name == "HarryTools.UI.Samples.HeroUI"), Is.False);
            Assert.That(GetType().Assembly.GetReferencedAssemblies().Any(reference => reference.Name == "HarryTools.UI.Samples.HeroUI"), Is.False);
        }

        [Test]
        public void FreshCoreControls_ResolveAndUpdatePaletteWithoutTemplatePrefabs()
        {
            var palette = ScriptableObject.CreateInstance<ColorPalette>();
            var parent = new GameObject("Core control palette test", typeof(RectTransform));
            parent.SetActive(false);
            try
            {
                palette.SetColor(ColorType.Accent, new Color(.1f, .3f, .7f, 1f));
                palette.SetColor(ColorType.Warning, new Color(.7f, .4f, .1f, 1f));
                palette.SetColor(ColorType.Surface, new Color(.1f, .12f, .14f, 1f));
                palette.SetContrast(ColorType.Surface, Color.white);
                palette.SetColor(ColorType.Danger, new Color(.8f, .1f, .2f, 1f));
                parent.AddComponent<PaletteProvider>().palette = palette;

                var buttonObject = Child(parent, "Button");
                var buttonFill = buttonObject.AddComponent<RectangleGraphic>();
                var button = buttonObject.AddComponent<ButtonElement>();
                References(button, ("_graphic", buttonFill));

                var toggleObject = Child(parent, "Toggle");
                var toggleFill = toggleObject.AddComponent<RectangleGraphic>();
                var knob = Child(toggleObject, "Knob").AddComponent<RectangleGraphic>();
                var toggle = toggleObject.AddComponent<ToggleElement>();
                References(toggle, ("_background", toggleFill), ("_nob", knob));
                var themedToggle = toggleObject.AddComponent<ThemedToggle>();
                References(themedToggle, ("_toggle", toggle), ("_background", toggleFill));

                var inputObject = Child(parent, "Input");
                var inputFill = inputObject.AddComponent<RectangleGraphic>();
                var input = inputObject.AddComponent<TMP_InputField>();
                var outline = inputObject.AddComponent<SelectedOutlineInputField>();
                References(outline, ("_input", input), ("_graphic", inputFill));
                var themedOutline = inputObject.AddComponent<ThemedInputOutline>();
                References(themedOutline, ("_outline", outline), ("_graphic", inputFill), ("_input", input));
                var label = Child(parent, "Required label").AddComponent<TextMeshProUGUI>();
                var required = inputObject.AddComponent<ThemedInputFieldMandatory>();
                References(required, ("_inputField", input), ("_graphic", outline), ("_label", label));
                var requiredData = new SerializedObject(required);
                requiredData.FindProperty("_labelFormat").stringValue = "Name <color=#{0}>*</color>";
                requiredData.ApplyModifiedPropertiesWithoutUndo();

                parent.SetActive(true);
                ThemeColors.Set(button, Info(ColorType.Accent), Info(ColorType.Warning), default(ColorTone), default(ColorTone));
                toggle.value = true;
                Invoke(themedToggle, "LateUpdate");
                AssertColor(button.backgroundNormal, palette.GetColor(ColorType.Accent));
                AssertColor(button.backgroundHover, palette.GetColor(ColorType.Warning));
                AssertColor(buttonFill.graphicColor, palette.GetColor(ColorType.Accent));
                AssertColor(toggleFill.graphicColor, palette.GetColor(ColorType.Accent));
                AssertColor(outline.outlineColor, palette.GetColor(ColorType.Accent));
                AssertMarker(label, palette);

                // A live palette change must reach all controls through the
                // core provider, with no template asset or template component.
                palette.SetColor(ColorType.Accent, new Color(.3f, .7f, .2f, 1f));
                palette.SetColor(ColorType.Warning, new Color(.2f, .6f, .8f, 1f));
                palette.SetContrast(ColorType.Surface, new Color(.15f, .17f, .19f, 1f));
                palette.SetColor(ColorType.Danger, new Color(.65f, .2f, .35f, 1f));
                Invoke(themedToggle, "LateUpdate");
                AssertColor(button.backgroundNormal, palette.GetColor(ColorType.Accent));
                AssertColor(button.backgroundHover, palette.GetColor(ColorType.Warning));
                AssertColor(buttonFill.graphicColor, palette.GetColor(ColorType.Accent));
                AssertColor(toggleFill.graphicColor, palette.GetColor(ColorType.Accent));
                AssertColor(outline.outlineColor, palette.GetColor(ColorType.Accent));
                AssertColor(label.color, palette.GetContrast(ColorType.Surface));
                AssertMarker(label, palette);
                var selection = palette.GetColor(ColorType.Accent);
                selection.a = new SerializedObject(themedOutline).FindProperty("_selectionOpacity").floatValue;
                AssertColor(input.selectionColor, selection);
            }
            finally
            {
                Object.DestroyImmediate(parent);
                Object.DestroyImmediate(palette);
            }
        }

        private static GameObject Child(GameObject parent, string name)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent.transform, false);
            return child;
        }

        private static void References(Object component, params (string field, Object value)[] references)
        {
            var data = new SerializedObject(component);
            foreach (var reference in references)
                data.FindProperty(reference.field).objectReferenceValue = reference.value;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Invoke(object target, string name)
        {
            var method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(target, null);
        }

        private static ColorInfo Info(ColorType role) => new ColorInfo { enabled = true, color = role };
        private static void AssertMarker(TMP_Text label, ColorPalette palette)
        {
            Assert.That(label.text, Is.EqualTo("Name <color=#" + ColorUtility.ToHtmlStringRGBA(palette.GetColor(ColorType.Danger)) + ">*</color>"));
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
