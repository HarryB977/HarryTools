using NUnit.Framework;
using UnityEngine;

namespace HarryTools.TextEffects.Tests {
    public class EffectParamBinderTests {
        // ==================== VARIABLES ===================
        #region Private Variables
        private enum Mode {
            Alpha,
            Beta
        }

        private sealed class Target {
            [SerializeField] private float amount = 1f;
            [SerializeField] private int count = 1;
            [SerializeField] private bool flag;
            [SerializeField] private Color tint = Color.white;
            [SerializeField] private Vector2 planar;
            [SerializeField] private Vector3 spatial;
            [SerializeField] private Mode mode = Mode.Alpha;
            public float Open = 1f;
            private float hidden = 1f;

            public float Amount => amount;
            public int Count => count;
            public bool Flag => flag;
            public Color Tint => tint;
            public Vector2 Planar => planar;
            public Vector3 Spatial => spatial;
            public Mode CurrentMode => mode;
            public float Hidden => hidden;
        }
        #endregion

        // ==================== TESTS ====================
        [Test]
        public void Binder_Float_Int_Bool_Color_Vector_Enum() {
            Target target = new();

            Assert.IsTrue(EffectParamBinder.TryApply(target, "amount", "2.5", out string error), error);
            Assert.AreEqual(2.5f, target.Amount);

            Assert.IsTrue(EffectParamBinder.TryApply(target, "COUNT", "7", out error), error);
            Assert.AreEqual(7, target.Count);

            Assert.IsTrue(EffectParamBinder.TryApply(target, "flag", "true", out error), error);
            Assert.IsTrue(target.Flag);

            Assert.IsTrue(EffectParamBinder.TryApply(target, "tint", "#FF0000", out error), error);
            Assert.AreEqual(Color.red, target.Tint);

            Assert.IsTrue(EffectParamBinder.TryApply(target, "planar", "1,2", out error), error);
            Assert.AreEqual(new Vector2(1f, 2f), target.Planar);

            Assert.IsTrue(EffectParamBinder.TryApply(target, "spatial", "1, 2, 3", out error), error);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), target.Spatial);

            Assert.IsTrue(EffectParamBinder.TryApply(target, "spatial", "4,5", out error), error);
            Assert.AreEqual(new Vector3(4f, 5f, 0f), target.Spatial);

            Assert.IsTrue(EffectParamBinder.TryApply(target, "mode", "beta", out error), error);
            Assert.AreEqual(Mode.Beta, target.CurrentMode);

            Assert.IsTrue(EffectParamBinder.TryApply(target, "open", "3", out error), error);
            Assert.AreEqual(3f, target.Open);
        }

        [Test]
        public void Binder_UnknownName_Error() {
            Target target = new();
            Assert.IsFalse(EffectParamBinder.TryApply(target, "missing", "1", out string error));
            Assert.IsNotEmpty(error);

            // Private fields without [SerializeField] are not exposed
            Assert.IsFalse(EffectParamBinder.TryApply(target, "hidden", "5", out error));
            Assert.AreEqual(1f, target.Hidden);
        }

        [Test]
        public void Binder_BadValue_Error() {
            Target target = new();
            Assert.IsFalse(EffectParamBinder.TryApply(target, "amount", "abc", out string error));
            Assert.IsNotEmpty(error);
            Assert.AreEqual(1f, target.Amount);

            Assert.IsFalse(EffectParamBinder.TryApply(target, "count", "1.5", out error));
            Assert.IsFalse(EffectParamBinder.TryApply(target, "flag", "maybe", out error));
            Assert.IsFalse(EffectParamBinder.TryApply(target, "tint", "notacolor", out error));
            Assert.IsFalse(EffectParamBinder.TryApply(target, "planar", "1,2,3", out error));
            Assert.IsFalse(EffectParamBinder.TryApply(target, "spatial", "1", out error));
            Assert.IsFalse(EffectParamBinder.TryApply(target, "mode", "gamma", out error));
            Assert.IsFalse(EffectParamBinder.TryApply(target, "mode", "5", out error));
            Assert.AreEqual(Mode.Alpha, target.CurrentMode);
        }

        [Test]
        public void Binder_InheritedFields_AreFound() {
            WaveEffect wave = new();
            Assert.IsTrue(EffectParamBinder.TryApply(wave, "duration", "2", out string error), error);
            Assert.AreEqual(2f, wave.Duration);
        }
    }
}
