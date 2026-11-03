using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HarryTools.UI.Tests
{
    public class TestOverlay : TestViewA { }

    public class TestOverlay2 : TestViewA { }

    public class ProgressOverlay : TestViewA, IProgress<float>
    {
        public readonly List<float> reports = new();
        public void Report(float value) => reports.Add(value);
    }

    public class OverlayLayerTests
    {
        private UITestFixture fixture;
        private UIRoot root;
        private OverlayLayer overlays;

        [SetUp]
        public void SetUp()
        {
            fixture = new UITestFixture();
            root = UIRoot.Create();
            overlays = root.Overlays;
            root.RegisterPrefab(fixture.CreateTemplate<TestOverlay>());
            root.RegisterPrefab(fixture.CreateTemplate<TestOverlay2>());
            root.RegisterPrefab(fixture.CreateTemplate<ProgressOverlay>());
        }

        [TearDown]
        public void TearDown()
        {
            if (root) Object.DestroyImmediate(root.gameObject);
            fixture.Dispose();
        }

        private bool OverlayHidden => overlays.Current == null || !overlays.Current.IsVisible;

        [Test]
        public void Show_SecondOverlay_HidesFirst()
        {
            var first = overlays.Show<TestOverlay>();
            var second = overlays.Show<TestOverlay2>();

            Assert.That(first.IsVisible, Is.False);
            Assert.That(second.IsVisible, Is.True);
            Assert.That(overlays.Current, Is.SameAs(second));
        }

        [Test]
        public void Hide_HidesCurrent()
        {
            var overlay = overlays.Show<TestOverlay>();

            overlays.Hide();

            Assert.That(overlay.IsVisible, Is.False);
            Assert.That(overlays.Current, Is.Null);
        }

        [Test]
        public void Hide_WithNoCurrent_DoesNothing()
        {
            Assert.DoesNotThrow(() => overlays.Hide());
            Assert.That(overlays.Current, Is.Null);
        }

        [UnityTest]
        public IEnumerator ShowWhile_UniTask_VisibleDuringAndHiddenAfter() => UniTask.ToCoroutine(async () =>
        {
            bool visibleDuring = false;

            async UniTask Work()
            {
                await UniTask.Yield();
                visibleDuring = overlays.Current is TestOverlay && overlays.Current.IsVisible;
                await UniTask.DelayFrame(2);
            }

            await overlays.ShowWhile<TestOverlay>(Work());

            Assert.That(visibleDuring, Is.True);
            Assert.That(OverlayHidden, Is.True);
        });

        [UnityTest]
        public IEnumerator ShowWhile_Generic_ReturnsResult() => UniTask.ToCoroutine(async () =>
        {
            async UniTask<int> Work()
            {
                await UniTask.Yield();
                return 42;
            }

            int result = await overlays.ShowWhile<TestOverlay, int>(Work());

            Assert.That(result, Is.EqualTo(42));
            Assert.That(OverlayHidden, Is.True);
        });

        [UnityTest]
        public IEnumerator ShowWhile_TaskThrows_HidesOverlayAndRethrows() => UniTask.ToCoroutine(async () =>
        {
            async UniTask Work()
            {
                await UniTask.Yield();
                throw new InvalidOperationException("boom");
            }

            InvalidOperationException caught = null;
            try { await overlays.ShowWhile<TestOverlay>(Work()); }
            catch (InvalidOperationException e) { caught = e; }

            Assert.That(caught, Is.Not.Null);
            Assert.That(caught.Message, Is.EqualTo("boom"));
            Assert.That(OverlayHidden, Is.True);
        });

        [UnityTest]
        public IEnumerator ShowWhile_Cancelled_HidesOverlay() => UniTask.ToCoroutine(async () =>
        {
            var cts = new CancellationTokenSource();
            var task = overlays.ShowWhile<TestOverlay>(UniTask.Delay(5000, DelayType.Realtime), cts.Token);
            await UniTask.Yield();
            Assert.That(overlays.Current is TestOverlay && overlays.Current.IsVisible, Is.True);

            cts.Cancel();
            bool cancelled = false;
            try { await task; }
            catch (OperationCanceledException) { cancelled = true; }

            Assert.That(cancelled, Is.True);
            Assert.That(OverlayHidden, Is.True);
        });

        [UnityTest]
        public IEnumerator ShowWhile_ProgressOverlay_ReceivesReportsAndEndsAtOne() => UniTask.ToCoroutine(async () =>
        {
            ProgressOverlay overlay = null;

            await overlays.ShowWhile<ProgressOverlay>(async (progress, ct) =>
            {
                overlay = (ProgressOverlay)overlays.Current;
                progress.Report(0.5f);
                await UniTask.Yield(ct);
            });

            Assert.That(overlay.reports, Is.EqualTo(new[] { 0.5f, 1f }));
            Assert.That(OverlayHidden, Is.True);
        });

        [UnityTest]
        public IEnumerator ShowWhile_AsyncOperation_HidesWhenDone() => UniTask.ToCoroutine(async () =>
        {
            AsyncOperation operation = Resources.UnloadUnusedAssets();

            await overlays.ShowWhile<ProgressOverlay>(operation);

            Assert.That(operation.isDone, Is.True);
            Assert.That(OverlayHidden, Is.True);
            var overlay = overlays.Get<ProgressOverlay>();
            Assert.That(overlay.reports[^1], Is.EqualTo(1f));
        });
    }
}
