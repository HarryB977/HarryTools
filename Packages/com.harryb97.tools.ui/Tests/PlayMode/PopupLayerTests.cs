using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HarryTools.UI.Tests
{
    public class TestPopup : TestViewA
    {
        public string text;
        public void Setup(string _text) => text = _text;
    }

    public class TestPopupNoQueue : TestViewA { }

    public class PopupLayerTests
    {
        private UITestFixture fixture;
        private UIRoot root;
        private PopupLayer popups;

        [SetUp]
        public void SetUp()
        {
            fixture = new UITestFixture();
            root = UIRoot.Create();
            popups = root.Popups;
            root.RegisterPrefab(fixture.CreateTemplate<TestPopup>());
            var noQueue = fixture.CreateTemplate<TestPopupNoQueue>();
            noQueue.RespectQueue = false;
            root.RegisterPrefab(noQueue);
        }

        [TearDown]
        public void TearDown()
        {
            if (root) Object.DestroyImmediate(root.gameObject);
            fixture.Dispose();
        }

        [Test]
        public void Show_WhileAnotherVisible_Queues_AndReturnsHiddenConfigurableInstance()
        {
            var p1 = popups.Show<TestPopup>();
            var p2 = popups.Show<TestPopup>();

            Assert.That(p1.IsVisible, Is.True);
            Assert.That(p2, Is.Not.Null);
            Assert.That(p2, Is.Not.SameAs(p1));
            Assert.That(p2.State, Is.EqualTo(ViewState.Hidden));
            Assert.That(popups.QueuedCount, Is.EqualTo(1));
        }

        [Test]
        public void HideTop_ShowsNextQueued()
        {
            var p1 = popups.Show<TestPopup>();
            var p2 = popups.Show<TestPopup>();

            p1.Hide();

            Assert.That(p2.IsVisible, Is.True);
            Assert.That(popups.Top, Is.SameAs(p2));
            Assert.That(popups.QueuedCount, Is.EqualTo(0));
        }

        [Test]
        public void SameType_TwoQueued_KeepTheirOwnData()
        {
            var a = popups.Show<TestPopup>();
            a.Setup("a");
            var b = popups.Show<TestPopup>();
            b.Setup("b");

            a.Hide();

            Assert.That(popups.Top, Is.SameAs(b));
            Assert.That(b.text, Is.EqualTo("b"));
            Assert.That(a.text, Is.EqualTo("a"));
        }

        [Test]
        public void RespectQueueFalse_ShowsImmediatelyOnTop()
        {
            popups.Show<TestPopup>();
            var urgent = popups.Show<TestPopupNoQueue>();

            Assert.That(urgent.IsVisible, Is.True);
            Assert.That(popups.Top, Is.SameAs(urgent));
            Assert.That(popups.QueuedCount, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator Hide_QueuedPopup_RemovesFromQueueWithoutShowing() => UniTask.ToCoroutine(async () =>
        {
            popups.Show<TestPopup>();
            var queued = popups.Show<TestPopup>();
            int shown = 0;
            queued.Shown += _ => shown++;

            queued.Hide();
            await queued.WaitUntilHidden().Timeout(System.TimeSpan.FromSeconds(1));

            Assert.That(popups.QueuedCount, Is.EqualTo(0));
            Assert.That(shown, Is.EqualTo(0));
            Assert.That(queued.shownCount, Is.EqualTo(0));
        });

        [Test]
        public void HideAll_ClearsQueue()
        {
            var p1 = popups.Show<TestPopup>();
            var p2 = popups.Show<TestPopup>();

            popups.HideAll();

            Assert.That(popups.QueuedCount, Is.EqualTo(0));
            Assert.That(p1.IsVisible, Is.False);
            Assert.That(p2.IsVisible, Is.False);
            Assert.That(popups.VisibleViews.Count, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator WaitUntilHiddenT_WaitsForQueuedToo() => UniTask.ToCoroutine(async () =>
        {
            var p1 = popups.Show<TestPopup>();
            var p2 = popups.Show<TestPopup>();
            bool done = false;
            popups.WaitUntilHidden<TestPopup>().ContinueWith(() => done = true).Forget();

            p1.Hide();
            await UniTask.DelayFrame(2);
            Assert.That(done, Is.False);

            p2.Hide();
            await UniTask.DelayFrame(2);
            Assert.That(done, Is.True);
        });

        [UnityTest]
        public IEnumerator Backdrop_VisibleWhilePopupVisible_HiddenAfterLast()
        {
            Assert.That(popups.IsBackdropVisible, Is.False);

            var p1 = popups.Show<TestPopup>();
            Assert.That(popups.IsBackdropVisible, Is.True);
            Assert.That(popups.Backdrop.blocksRaycasts, Is.True);
            Assert.That(popups.Backdrop.GetComponent<Canvas>().sortingOrder, Is.LessThan(p1.Canvas.sortingOrder));

            p1.Hide();
            Assert.That(popups.IsBackdropVisible, Is.False);
            Assert.That(popups.Backdrop.blocksRaycasts, Is.False);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(popups.Backdrop.alpha, Is.EqualTo(0f));
        }

        [Test]
        public void BackdropClick_HidesTop_UnlessDisabled()
        {
            var p1 = popups.Show<TestPopup>();
            var backdrop = popups.Backdrop.GetComponent<PopupBackdrop>();

            p1.HideOnBackdropClick = false;
            backdrop.OnPointerClick(new PointerEventData(EventSystem.current));
            Assert.That(p1.IsVisible, Is.True);

            p1.HideOnBackdropClick = true;
            backdrop.OnPointerClick(new PointerEventData(EventSystem.current));
            Assert.That(p1.IsVisible, Is.False);
        }

        [Test]
        public void HiddenCachedPopup_IsReusedByNextShow()
        {
            var p1 = popups.Show<TestPopup>();
            p1.Hide();

            var again = popups.Show<TestPopup>();

            Assert.That(again, Is.SameAs(p1));
            Assert.That(again.IsVisible, Is.True);
        }
    }
}
