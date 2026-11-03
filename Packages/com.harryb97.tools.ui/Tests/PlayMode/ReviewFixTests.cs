using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HarryTools.UI.Tests
{
    [Serializable]
    public class ThrowingTransition : IViewTransition
    {
        public bool throwOnEnter, throwOnExit;
        public UniTask Enter(View _view, CancellationToken _ct) => throwOnEnter ? throw new InvalidOperationException("enter boom") : UniTask.CompletedTask;
        public UniTask Exit(View _view, CancellationToken _ct) => throwOnExit ? throw new InvalidOperationException("exit boom") : UniTask.CompletedTask;
    }

    public class PreloadedOverlay : TestViewA { }

    /// <summary> Regresiones encontradas en la revisión final </summary>
    public class ReviewFixTests
    {
        private UITestFixture fixture;
        private UIRoot root;

        [SetUp]
        public void SetUp()
        {
            fixture = new UITestFixture();
            root = UIRoot.Create();
            root.RegisterPrefab(fixture.CreateTemplate<TestPopup>());
            root.RegisterPrefab(fixture.CreateTemplate<TestOverlay>());
            root.RegisterPrefab(fixture.CreateTemplate<TestOverlay2>());
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var r in Object.FindObjectsByType<UIRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(r.gameObject);
            fixture.Dispose();
        }

        // C1
        [UnityTest]
        public IEnumerator WaitUntilHidden_QueuedPopup_WaitsUntilShownAndHidden() => UniTask.ToCoroutine(async () =>
        {
            var first = UI.Popups.Show<TestPopup>();
            var queued = UI.Popups.Show<TestPopup>();
            Assert.That(queued.IsQueued, Is.True);
            bool done = false;
            queued.WaitUntilHidden().ContinueWith(() => done = true).Forget();

            await UniTask.DelayFrame(2);
            Assert.That(done, Is.False, "returned while still queued");

            first.Hide();
            await UniTask.DelayFrame(2);
            Assert.That(queued.IsVisible, Is.True);
            Assert.That(done, Is.False, "returned while visible");

            queued.Hide();
            await UniTask.DelayFrame(2);
            Assert.That(done, Is.True);
        });

        // I1
        [Test]
        public void ThrowingEnterTransition_EndsVisibleAndLogs()
        {
            LogAssert.Expect(LogType.Error, new Regex("enter boom"));
            var template = fixture.CreateTemplate<TestViewB>(ViewLifetime.Cached, new ThrowingTransition { throwOnEnter = true });

            var view = UI.Views.Show(template);

            Assert.That(view.State, Is.EqualTo(ViewState.Visible));
            Assert.That(view.CanvasGroup.interactable, Is.True);
        }

        [Test]
        public void ThrowingExitTransition_EndsHidden_AndQueueContinues()
        {
            LogAssert.Expect(LogType.Error, new Regex("exit boom"));
            var first = UI.Popups.Show<TestPopup>();
            first.Transition = new ThrowingTransition { throwOnExit = true };
            var queued = UI.Popups.Show<TestPopup>();

            first.Hide();

            Assert.That(first.State, Is.EqualTo(ViewState.Hidden));
            Assert.That(queued.IsVisible, Is.True);
        }

        // I2
        [UnityTest]
        public IEnumerator ShowFromHiddenHandler_KeepsLayerConsistent()
        {
            var template = fixture.CreateTemplate<TestViewC>(ViewLifetime.Transient);
            var view = UI.Views.Show(template);
            bool reshown = false;
            view.Hidden += v => { if (reshown) return; reshown = true; v.Show(); };

            view.Hide();
            yield return null;

            Assert.That(view != null, Is.True);
            Assert.That(view.IsVisible, Is.True);
            Assert.That(UI.Views.VisibleViews, Has.Exactly(1).SameAs(view));
            Assert.DoesNotThrow(() => UI.Views.Show(fixture.CreateTemplate<TestViewB>()));
        }

        [Test]
        public void HideFromShownHandler_RaisesLayerEventsInOrder()
        {
            var events = new List<string>();
            UI.Views.ViewShown += _ => events.Add("shown");
            UI.Views.ViewHidden += _ => events.Add("hidden");
            var template = fixture.CreateTemplate<TestViewC>();
            var view = UI.Views.Show(template);
            view.Hide();
            events.Clear();
            view.Shown += v => v.Hide();

            UI.Views.Show(view);

            Assert.That(events, Is.EqualTo(new[] { "shown", "hidden" }));
            Assert.That(view.IsVisible, Is.False);
        }

        // I3
        [UnityTest]
        public IEnumerator ShowWhile_NestedSameOverlay_StaysUntilOuterEnds() => UniTask.ToCoroutine(async () =>
        {
            bool visibleAfterInner = false;
            async UniTask Outer()
            {
                await UI.Overlays.ShowWhile<TestOverlay>(UniTask.DelayFrame(2));
                visibleAfterInner = UI.Overlays.Current is TestOverlay && UI.Overlays.Current.IsVisible;
                await UniTask.DelayFrame(2);
            }

            await UI.Overlays.ShowWhile<TestOverlay>(UniTask.Defer(Outer));

            Assert.That(visibleAfterInner, Is.True);
            Assert.That(UI.Overlays.Current, Is.Null);
        });

        [UnityTest]
        public IEnumerator ShowWhile_NestedDifferentOverlay_RestoresOuter() => UniTask.ToCoroutine(async () =>
        {
            bool outerBack = false;
            async UniTask Outer()
            {
                await UI.Overlays.ShowWhile<TestOverlay2>(UniTask.DelayFrame(2));
                outerBack = UI.Overlays.Current is TestOverlay && UI.Overlays.Current.IsVisible;
            }

            await UI.Overlays.ShowWhile<TestOverlay>(UniTask.Defer(Outer));

            Assert.That(outerBack, Is.True);
            Assert.That(UI.Overlays.Current, Is.Null);
        });

        // I4
        [Test]
        public void EnteringTopView_BlocksRaycastsButIsNotInteractable()
        {
            var template = fixture.CreateTemplate<TestViewB>(ViewLifetime.Cached, new FadeTransition { duration = 0.5f });

            var view = UI.Overlays.Show(template);

            Assert.That(view.State, Is.EqualTo(ViewState.Entering));
            Assert.That(view.CanvasGroup.blocksRaycasts, Is.True);
            Assert.That(view.CanvasGroup.interactable, Is.False);
        }

        // I5
        [UnityTest]
        public IEnumerator ShowAsync_Cancelled_LeavesNoStrayInstance() => UniTask.ToCoroutine(async () =>
        {
            root.RegisterPrefab(fixture.CreateTemplate<TestViewC>());
            var cts = new CancellationTokenSource();
            cts.Cancel();

            bool cancelled = false;
            try { await UI.Views.ShowAsync<TestViewC>(cts.Token); }
            catch (OperationCanceledException) { cancelled = true; }
            await UniTask.DelayFrame(5);

            Assert.That(cancelled, Is.True);
            Assert.That(UI.Views.GetComponentsInChildren<TestViewC>(true), Is.Empty);
        });

        // I6
        [Test]
        public void Preloaded_UsesTheViewsDefaultLayer()
        {
            Object.DestroyImmediate(root.gameObject);
            var template = fixture.CreateTemplate<PreloadedOverlay>(ViewLifetime.Preloaded);
            template.DefaultLayer = UILayerKind.Overlays;
            var collection = ScriptableObject.CreateInstance<ViewCollection>();
            collection.autoGenerate = false;
            collection.views = new View[] { template };

            root = UIRoot.Create("Root", collection);

            Assert.That(UI.Overlays.Get<PreloadedOverlay>(), Is.Not.Null);
            Assert.That(UI.Views.Get<PreloadedOverlay>(), Is.Null);
            Object.DestroyImmediate(collection);
        }

        // I7
        [UnityTest]
        public IEnumerator ShowAsync_QueuedThenReleased_Completes() => UniTask.ToCoroutine(async () =>
        {
            UI.Popups.Show<TestPopup>();
            bool done = false;
            UI.Popups.ShowAsync<TestPopup>().ContinueWith(_ => done = true).Forget();
            await UniTask.DelayFrame(2);
            Assert.That(done, Is.False);

            UI.Popups.HideAll();
            await UniTask.DelayFrame(3);

            Assert.That(done, Is.True);
        });

        // M1
        [UnityTest]
        public IEnumerator ShowAgain_VisibleView_OnlyMovesToTop() => UniTask.ToCoroutine(async () =>
        {
            var a = UI.Views.Show(fixture.CreateTemplate<TestViewC>(ViewLifetime.Cached, new FadeTransition { duration = 0.1f }));
            await a.WaitUntilShown();
            UI.Views.Show(fixture.CreateTemplate<TestViewB>());
            int shown = a.shownCount;

            UI.Views.Show(a);

            Assert.That(UI.Views.Top, Is.SameAs(a));
            Assert.That(a.State, Is.EqualTo(ViewState.Visible));
            Assert.That(a.shownCount, Is.EqualTo(shown));
            Assert.That(a.CanvasGroup.alpha, Is.EqualTo(1f));
        });

        // M5
        [Test]
        public void DestroyedQueuedPopup_DoesNotStallQueue()
        {
            var first = UI.Popups.Show<TestPopup>();
            var queued = UI.Popups.Show<TestPopup>();
            var third = UI.Popups.Show<TestPopup>();
            Object.DestroyImmediate(queued.gameObject);

            Assert.DoesNotThrow(() => first.Hide());
            Assert.That(third.IsVisible, Is.True);
            Assert.That(UI.Popups.QueuedCount, Is.EqualTo(0));
        }
    }
}
