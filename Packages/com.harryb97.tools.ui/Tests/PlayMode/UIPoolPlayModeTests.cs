using System.Collections;
using NUnit.Framework;
using HarryTools.UI;
using UnityEngine;
using UnityEngine.TestTools;

namespace HarryTools.UI.Tests
{
    public class UIPoolPlayModeTests
    {
        [UnityTest]
        public IEnumerator Dispose_DestroysAllInstances()
        {
            var parentGo = new GameObject("PoolParent", typeof(RectTransform));
            var prefabGo = new GameObject("PoolPrefab", typeof(RectTransform));

            var pool = new UIPool<RectTransform>(
                prefabGo.GetComponent<RectTransform>(),
                parentGo.GetComponent<RectTransform>());

            var a = pool.GetInstance();
            var b = pool.GetInstance();

            pool.Dispose();
            yield return null;

            Assert.IsTrue(a == null);
            Assert.IsTrue(b == null);

            Object.Destroy(parentGo);
            Object.Destroy(prefabGo);
        }
    }
}
