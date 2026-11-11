using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace HarryTools.SceneRefs.Tests
{
    public class SceneIdNamingTests
    {
        // ==================== SANITIZE ====================

        [TestCase("Puerta (1)", "Puerta_1")]
        [TestCase("  Manzana Roja!! ", "Manzana_Roja")]
        [TestCase("???", "Object")]
        public void Sanitize(string _in, string _out) => Assert.AreEqual(_out, SceneIdNaming.Sanitize(_in));

        // ==================== INCREMENT ====================

        [TestCase("Manzana01", "Manzana02")]
        [TestCase("Manzana09", "Manzana10")]
        [TestCase("Manzana99", "Manzana100")]
        [TestCase("Puerta", "Puerta1")]
        [TestCase("9", "10")]
        [TestCase("Item0", "Item1")]
        // Longer than any number type: incremented digit by digit.
        [TestCase("Big123456789012345678901234567899", "Big123456789012345678901234567900")]
        [TestCase("Big999999999999999999999999999999", "Big1000000000000000000000000000000")]
        public void Increment(string _in, string _out) => Assert.AreEqual(_out, SceneIdNaming.Increment(_in));

        // ==================== MAKE UNIQUE ====================

        [Test]
        public void MakeUnique_IgnoresCase()
        {
            HashSet<string> _taken = new HashSet<string>(SceneIdNaming.Comparer) { "manzana01", "Manzana02" };
            Assert.AreEqual("Manzana03", SceneIdNaming.MakeUnique("Manzana01", _taken.Contains));
        }

        [Test]
        public void MakeUnique_FreeIdIsKept()
        {
            Assert.AreEqual("Puerta", SceneIdNaming.MakeUnique("Puerta", _id => false));
        }

        // ==================== GUID ====================

        [Test]
        public void NewGuid_Is32Hex() => StringAssert.IsMatch("^[0-9a-f]{32}$", SceneIdNaming.NewGuid());

        // ==================== COMPONENT ====================

        [Test]
        public void SceneId_IsHiddenInInspector()
        {
            GameObject _go = new GameObject("SceneIdHideTest");
            try
            {
                SceneId _sceneId = _go.AddComponent<SceneId>();
                _sceneId.Set("g", "i");
                Assert.IsTrue((_sceneId.hideFlags & HideFlags.HideInInspector) != 0);
                Assert.AreEqual("g", _sceneId.Guid);
                Assert.AreEqual("i", _sceneId.Id);
            }
            finally
            {
                Object.DestroyImmediate(_go);
            }
        }
    }
}
