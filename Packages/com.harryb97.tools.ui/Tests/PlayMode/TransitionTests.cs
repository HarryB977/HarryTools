using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarryTools.UI.Tests
{
    public class TransitionTests
    {
        private UITestFixture fixture;
        private TestViewA view;
        private RectTransform content;

        [SetUp]
        public void SetUp()
        {
            fixture = new UITestFixture();
            var canvas = fixture.CreateCanvas();
            view = fixture.CreateTemplate<TestViewA>();
            view.transform.SetParent(canvas.transform, false);
            content = (RectTransform)view.transform.GetChild(0);
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            fixture.Dispose();
        }

        [UnityTest]
        public IEnumerator Fade_Enter_EndsAtAlphaOne() => UniTask.ToCoroutine(async () =>
        {
            view.CanvasGroup.alpha = 0f;
            await new FadeTransition { duration = 0.05f }.Enter(view, CancellationToken.None);
            Assert.That(view.CanvasGroup.alpha, Is.EqualTo(1f));
        });

        [UnityTest]
        public IEnumerator Fade_Exit_EndsAtAlphaZero() => UniTask.ToCoroutine(async () =>
        {
            await new FadeTransition { duration = 0.05f }.Exit(view, CancellationToken.None);
            Assert.That(view.CanvasGroup.alpha, Is.EqualTo(0f));
        });

        [UnityTest]
        public IEnumerator Slide_EnterFromLeft_StartsOffsetAndEndsAtZero() => UniTask.ToCoroutine(async () =>
        {
            var task = new SlideTransition { from = SlideDirection.Left, duration = 0.1f }.Enter(view, CancellationToken.None);
            await UniTask.Yield();
            Assert.That(content.anchoredPosition.x, Is.LessThan(0f));
            await task;
            Assert.That(content.anchoredPosition, Is.EqualTo(Vector2.zero));
        });

        [UnityTest]
        public IEnumerator Parallel_CompletesWhenLongestChildCompletes() => UniTask.ToCoroutine(async () =>
        {
            var parallel = new ParallelTransition
            {
                transitions = new IViewTransition[] { new FadeTransition { duration = 0.05f }, new ScaleTransition { duration = 0.15f } }
            };
            float start = Time.unscaledTime;
            await parallel.Enter(view, CancellationToken.None);
            Assert.That(Time.unscaledTime - start, Is.GreaterThanOrEqualTo(0.14f));
            Assert.That(content.localScale, Is.EqualTo(Vector3.one));
        });

        [UnityTest]
        public IEnumerator Enter_Cancelled_ThrowsOperationCanceledAndStopsTween() => UniTask.ToCoroutine(async () =>
        {
            var cts = new CancellationTokenSource();
            view.CanvasGroup.alpha = 0f;
            var task = new FadeTransition { duration = 1f }.Enter(view, cts.Token);
            await UniTask.Yield();
            cts.Cancel();
            bool cancelled = false;
            try { await task; }
            catch (OperationCanceledException) { cancelled = true; }
            float alphaAtCancel = view.CanvasGroup.alpha;
            await UniTask.DelayFrame(3);
            Assert.That(cancelled, Is.True);
            Assert.That(view.CanvasGroup.alpha, Is.EqualTo(alphaAtCancel));
            Assert.That(alphaAtCancel, Is.LessThan(1f));
        });

        [UnityTest]
        public IEnumerator Transitions_IgnoreTimeScale() => UniTask.ToCoroutine(async () =>
        {
            Time.timeScale = 0f;
            view.CanvasGroup.alpha = 0f;
            await new FadeTransition { duration = 0.05f }.Enter(view, CancellationToken.None).Timeout(TimeSpan.FromSeconds(2), DelayType.Realtime);
            Assert.That(view.CanvasGroup.alpha, Is.EqualTo(1f));
        });
    }
}
