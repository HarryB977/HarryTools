using System.Collections;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarryTools.UI.Tests
{
    public class ViewLayerTests
    {
        private UITestFixture fixture;
        private ViewLayer layer;
        private TestViewA templateA;
        private TestViewB templateB;

        [SetUp]
        public void SetUp()
        {
            fixture = new UITestFixture();
            layer = fixture.CreateLayer();
            templateA = fixture.CreateTemplate<TestViewA>();
            templateB = fixture.CreateTemplate<TestViewB>();
        }

        [TearDown]
        public void TearDown() => fixture.Dispose();

        [Test]
        public void Show_Prefab_InstantiatesVisibleOnTop()
        {
            var view = layer.Show(templateA);

            Assert.That(view, Is.Not.Null);
            Assert.That(view, Is.Not.SameAs(templateA));
            Assert.That(view.Layer, Is.SameAs(layer));
            Assert.That(layer.Top, Is.SameAs(view));
            Assert.That(view.IsVisible, Is.True);
            Assert.That(view.State, Is.EqualTo(ViewState.Visible));
            Assert.That(view.Canvas.enabled, Is.True);
        }

        [Test]
        public void Show_TwoViews_OnlyTopIsInteractable()
        {
            var a = layer.Show(templateA);
            var b = layer.Show(templateB);

            Assert.That(a.CanvasGroup.blocksRaycasts, Is.False);
            Assert.That(a.CanvasGroup.interactable, Is.False);
            Assert.That(b.CanvasGroup.blocksRaycasts, Is.True);
            Assert.That(b.Canvas.sortingOrder, Is.GreaterThan(a.Canvas.sortingOrder));
            Assert.That(a.backgroundCount, Is.EqualTo(1));
        }

        [Test]
        public void Hide_Top_RestoresPreviousToForeground()
        {
            var a = layer.Show(templateA);
            var b = layer.Show(templateB);
            int before = a.foregroundCount;

            b.Hide();

            Assert.That(layer.Top, Is.SameAs(a));
            Assert.That(a.foregroundCount, Is.EqualTo(before + 1));
            Assert.That(a.CanvasGroup.blocksRaycasts, Is.True);
            Assert.That(a.CanvasGroup.interactable, Is.True);
        }

        [Test]
        public void Hide_Cached_KeepsGameObjectActiveWithCanvasDisabled()
        {
            var view = layer.Show(templateA);
            view.Hide();

            Assert.That(view != null, Is.True);
            Assert.That(view.gameObject.activeSelf, Is.True);
            Assert.That(view.Canvas.enabled, Is.False);
            Assert.That(view.State, Is.EqualTo(ViewState.Hidden));
            Assert.That(view.CanvasGroup.blocksRaycasts, Is.False);
            Assert.That(view.hiddenCount, Is.EqualTo(1));
            Assert.That(layer.Show(templateA), Is.SameAs(view));
        }

        [UnityTest]
        public IEnumerator Hide_Transient_DestroysAfterExit()
        {
            var transient = fixture.CreateTemplate<TestViewC>(ViewLifetime.Transient);
            var view = layer.Show(transient);
            view.Hide();
            yield return null;

            Assert.That(view == null, Is.True);
            Assert.That(layer.Get<TestViewC>(), Is.Null);
        }

        [Test]
        public void Show_AlreadyVisible_MovesToTop()
        {
            var a = layer.Show(templateA);
            var b = layer.Show(templateB);

            var again = layer.Show(templateA);

            Assert.That(again, Is.SameAs(a));
            Assert.That(layer.Top, Is.SameAs(a));
            Assert.That(layer.VisibleViews.Count, Is.EqualTo(2));
            Assert.That(a.Canvas.sortingOrder, Is.GreaterThan(b.Canvas.sortingOrder));
        }

        [Test]
        public void CullViewsBehind_DisablesCanvasesBelow_AndRestoresOnHide()
        {
            templateB.CullViewsBehind = true;
            var a = layer.Show(templateA);
            var b = layer.Show(templateB);

            Assert.That(a.Canvas.enabled, Is.False);
            Assert.That(a.State, Is.EqualTo(ViewState.Visible));

            b.Hide();

            Assert.That(a.Canvas.enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator Hide_DuringEnterTransition_EndsHiddenAndRaisesHiddenOnce() => UniTask.ToCoroutine(async () =>
        {
            var slow = fixture.CreateTemplate<TestViewC>(ViewLifetime.Cached, new FadeTransition { duration = 0.2f });
            var view = layer.Show(slow);
            int hiddenEvents = 0;
            view.Hidden += _ => hiddenEvents++;

            await UniTask.Yield();
            Assert.That(view.State, Is.EqualTo(ViewState.Entering));
            view.Hide();
            await view.WaitUntilHidden();
            await UniTask.Delay(300, DelayType.Realtime);

            Assert.That(view.State, Is.EqualTo(ViewState.Hidden));
            Assert.That(view.Canvas.enabled, Is.False);
            Assert.That(hiddenEvents, Is.EqualTo(1));
        });

        [Test]
        public void Show_UnknownType_ReturnsNullAndWarns()
        {
            LogAssert.Expect(LogType.Warning, new Regex("TestViewB"));

            var view = layer.Show<TestViewB>();

            Assert.That(view, Is.Null);
            Assert.That(layer.VisibleViews.Count, Is.EqualTo(0));
        }

        [Test]
        public void Adopt_SceneChild_IsHiddenAndShowable()
        {
            var canvas = fixture.CreateCanvas("Canvas2");
            var go = new GameObject("Layer2", typeof(RectTransform));
            go.SetActive(false);
            go.transform.SetParent(canvas.transform, false);
            var placed = new GameObject("Placed", typeof(RectTransform)).AddComponent<TestViewA>();
            placed.transform.SetParent(go.transform, false);
            var layer2 = go.AddComponent<ViewLayer>();
            go.SetActive(true);

            Assert.That(placed.Layer, Is.SameAs(layer2));
            Assert.That(placed.State, Is.EqualTo(ViewState.Hidden));
            Assert.That(placed.Canvas.enabled, Is.False);

            var shown = layer2.Show<TestViewA>();
            Assert.That(shown, Is.SameAs(placed));
            Assert.That(shown.IsVisible, Is.True);
            Assert.That(go.transform.childCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ShowAsync_AwaitsEnterTransition() => UniTask.ToCoroutine(async () =>
        {
            var slow = fixture.CreateTemplate<TestViewC>(ViewLifetime.Cached, new FadeTransition { duration = 0.1f });
            layer.RegisterPrefab(slow);

            var view = await layer.ShowAsync<TestViewC>();

            Assert.That(view, Is.Not.Null);
            Assert.That(view, Is.Not.SameAs(slow));
            Assert.That(view.State, Is.EqualTo(ViewState.Visible));
            Assert.That(view.CanvasGroup.interactable, Is.True);
        });

        [UnityTest]
        public IEnumerator WaitUntilHidden_CompletesAfterExit() => UniTask.ToCoroutine(async () =>
        {
            var slow = fixture.CreateTemplate<TestViewC>(ViewLifetime.Cached, new FadeTransition { duration = 0.1f });
            var view = layer.Show(slow);
            await view.WaitUntilShown();

            layer.Hide<TestViewC>();
            Assert.That(view.State, Is.EqualTo(ViewState.Exiting));
            await layer.WaitUntilHidden<TestViewC>();

            Assert.That(view.State, Is.EqualTo(ViewState.Hidden));
        });
    }
}
