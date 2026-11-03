using System;
using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HarryTools.UI.Tests
{
    public class UIRootTests
    {
        private UITestFixture fixture;
        private UIRoot root;

        [SetUp]
        public void SetUp() => fixture = new UITestFixture();

        [TearDown]
        public void TearDown()
        {
            foreach (var r in Object.FindObjectsByType<UIRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(r.gameObject);
            fixture.Dispose();
        }

        [Test]
        public void UI_WithoutRoot_ThrowsInvalidOperation()
        {
            Assert.That(UI.HasRoot, Is.False);
            var ex = Assert.Throws<InvalidOperationException>(() => UI.Show<TestViewA>());
            Assert.That(ex.Message, Does.Contain("UIRoot"));
        }

        [Test]
        public void UI_Show_UsesRegisteredPrefab()
        {
            root = UIRoot.Create();
            root.RegisterPrefab(fixture.CreateTemplate<TestViewA>());

            var view = UI.Show<TestViewA>();

            Assert.That(view, Is.Not.Null);
            Assert.That(view.Layer, Is.SameAs(root.Views));
            Assert.That(view.Layer.Kind, Is.EqualTo(UILayerKind.Views));
        }

        [Test]
        public void SecondRoot_IsIgnoredAndDeactivated()
        {
            root = UIRoot.Create("First");
            LogAssert.Expect(LogType.Warning, new Regex("UIRoot"));

            var second = UIRoot.Create("Second");

            Assert.That(UI.Root, Is.SameAs(root));
            Assert.That(second.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator UI_AfterRootDestroyed_NewRootRegisters()
        {
            root = UIRoot.Create("First");
            Object.Destroy(root.gameObject);
            yield return null;

            Assert.That(UI.HasRoot, Is.False);
            var next = UIRoot.Create("Next");
            Assert.That(UI.Root, Is.SameAs(next));
        }

        [Test]
        public void Preloaded_IsInstantiatedOnAwake_Hidden()
        {
            var collection = ScriptableObject.CreateInstance<ViewCollection>();
            collection.autoGenerate = false;
            collection.views = new View[] { fixture.CreateTemplate<TestViewB>(ViewLifetime.Preloaded) };

            root = UIRoot.Create("Root", collection);

            var view = UI.Get<TestViewB>();
            Assert.That(view, Is.Not.Null);
            Assert.That(view.IsVisible, Is.False);
            Assert.That(view.Layer, Is.SameAs(root.Views));
            Object.DestroyImmediate(collection);
        }

        [Test]
        public void UI_Hide_FindsViewInAnyLayer()
        {
            root = UIRoot.Create();
            var view = UI.Overlays.Show(fixture.CreateTemplate<TestViewC>());

            Assert.That(UI.IsVisible<TestViewC>(), Is.True);
            Assert.That(UI.Hide<TestViewC>(), Is.True);
            Assert.That(view.IsVisible, Is.False);
        }

        [Test]
        public void UI_ViewShownEvent_ForwardsFromLayers()
        {
            root = UIRoot.Create();
            View shown = null, hidden = null;
            Action<View> onShown = v => shown = v, onHidden = v => hidden = v;
            UI.ViewShown += onShown;
            UI.ViewHidden += onHidden;
            try
            {
                var view = UI.Popups.Show(fixture.CreateTemplate<TestViewA>());
                Assert.That(shown, Is.SameAs(view));
                view.Hide();
                Assert.That(hidden, Is.SameAs(view));
            }
            finally
            {
                UI.ViewShown -= onShown;
                UI.ViewHidden -= onHidden;
            }
        }

        [Test]
        public void Layers_SortingBands_OverlayAbovePopupAboveView()
        {
            root = UIRoot.Create();
            var v = UI.Views.Show(fixture.CreateTemplate<TestViewA>());
            var p = UI.Popups.Show(fixture.CreateTemplate<TestViewB>());
            var o = UI.Overlays.Show(fixture.CreateTemplate<TestViewC>());

            Assert.That(p.Canvas.sortingOrder, Is.GreaterThan(v.Canvas.sortingOrder));
            Assert.That(o.Canvas.sortingOrder, Is.GreaterThan(p.Canvas.sortingOrder));
        }

        [Test]
        public void View_ShowWithoutLayer_GoesToViewsLayer()
        {
            root = UIRoot.Create();
            var template = fixture.CreateTemplate<TestViewA>();

            template.Show();

            Assert.That(UI.Views.Get<TestViewA>(), Is.Not.Null);
            Assert.That(UI.IsVisible<TestViewA>(), Is.True);
        }

        [Test]
        public void Root_IsPaletteProvider()
        {
            var palette = ScriptableObject.CreateInstance<ColorPalette>();
            palette.SetColor(ColorType.Accent, Color.magenta);
            root = UIRoot.Create();
            root.palette = palette;

            var template = fixture.CreateTemplate<TestViewA>();
            var label = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            label.transform.SetParent(template.transform.GetChild(0), false);
            var colored = label.gameObject.AddComponent<ColoredGraphic>();
            colored.transitionDuration = 0f;
            colored.SetColor(new ColorInfo { enabled = true, color = ColorType.Accent });

            var view = UI.Show(template);
            var instanceLabel = view.GetComponentInChildren<TextMeshProUGUI>();
            view.GetComponentInChildren<ColoredGraphic>().Refresh();

            Assert.That(instanceLabel.color, Is.EqualTo(Color.magenta));
            Object.DestroyImmediate(palette);
        }
    }
}
