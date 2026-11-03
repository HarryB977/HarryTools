using NUnit.Framework;
using HarryTools.UI;
using UnityEngine;

namespace HarryTools.UI.Tests
{
    public class UIPoolTests
    {
        private GameObject _parentGo;
        private GameObject _prefabGo;
        private RectTransform _parent;
        private RectTransform _prefab;
        private UIPool<RectTransform> _pool;

        [SetUp]
        public void SetUp()
        {
            _parentGo = new GameObject("PoolParent", typeof(RectTransform));
            _prefabGo = new GameObject("PoolPrefab", typeof(RectTransform));
            _parent = _parentGo.GetComponent<RectTransform>();
            _prefab = _prefabGo.GetComponent<RectTransform>();
            _pool = new UIPool<RectTransform>(_prefab, _parent);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_parentGo);
            Object.DestroyImmediate(_prefabGo);
        }

        [Test]
        public void GetInstance_CreatesParentedInstance()
        {
            var instance = _pool.GetInstance();
            Assert.IsNotNull(instance);
            Assert.AreSame(_parent, instance.parent);
            Assert.IsTrue(instance.gameObject.activeSelf);
        }

        [Test]
        public void GetInstance_CreatesDistinctInstances()
        {
            var a = _pool.GetInstance();
            var b = _pool.GetInstance();
            Assert.AreNotSame(a, b);
        }

        [Test]
        public void ResetCounter_ReusesExistingInstances()
        {
            var a = _pool.GetInstance();
            _pool.ResetCounter();
            var b = _pool.GetInstance();
            Assert.AreSame(a, b);
        }

        [Test]
        public void DiscardRest_DeactivatesUnusedInstances()
        {
            var a = _pool.GetInstance();
            var b = _pool.GetInstance();

            _pool.ResetCounter();
            var reused = _pool.GetInstance();
            Assert.AreSame(a, reused);

            _pool.DiscardRest();

            Assert.IsTrue(a.gameObject.activeSelf);
            Assert.IsFalse(b.gameObject.activeSelf);
        }

        [Test]
        public void GetInstance_ReactivatesDiscardedInstance()
        {
            var a = _pool.GetInstance();
            _pool.ResetCounter();
            _pool.DiscardRest();
            Assert.IsFalse(a.gameObject.activeSelf);

            var reused = _pool.GetInstance();
            Assert.AreSame(a, reused);
            Assert.IsTrue(a.gameObject.activeSelf);
        }

        [Test]
        public void GetInstance_PrunesExternallyDestroyedInstances()
        {
            var a = _pool.GetInstance();
            _pool.ResetCounter();
            Object.DestroyImmediate(a.gameObject);

            var b = _pool.GetInstance();
            Assert.IsNotNull(b);
            Assert.IsTrue(b);
        }
    }
}
