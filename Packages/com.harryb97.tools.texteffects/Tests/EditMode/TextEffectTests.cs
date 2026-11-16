using System.Collections.Generic;
using NUnit.Framework;
using Unity.Profiling;
using UnityEngine;

namespace HarryTools.TextEffects.Tests {
    public class TextEffectTests {
        // ==================== VARIABLES ===================
        #region Private Variables
        private const float Tolerance = 0.0001f;
        #endregion

        // ==================== HELPERS ====================
        #region Helpers
        private static T Make<T>(params string[] _overrides) where T : TextEffect, new() {
            T effect = new();
            List<EffectParam> list = new();
            for (int i = 0; i < _overrides.Length; i += 2) list.Add(new EffectParam(_overrides[i], _overrides[i + 1]));
            List<TextIssue> issues = new();
            TextEffect result = effect.CloneWithOverrides(list, issues);
            Assert.IsEmpty(issues, "Test setup overrides must be valid");
            return (T)result;
        }

        private static CharacterState Quad(int _index = 0) {
            return new CharacterState {
                BottomLeft = new Vector3(0f, 0f, 0f),
                TopLeft = new Vector3(0f, 2f, 0f),
                TopRight = new Vector3(2f, 2f, 0f),
                BottomRight = new Vector3(2f, 0f, 0f),
                ColorBL = new Color32(255, 255, 255, 255),
                ColorTL = new Color32(255, 255, 255, 255),
                ColorTR = new Color32(255, 255, 255, 255),
                ColorBR = new Color32(255, 255, 255, 255),
                Index = _index,
                IndexInSpan = _index,
                SpanLength = 10,
                Size = 2f
            };
        }

        private static EffectTime At(float _progress, float _global = 0f) => new(_progress, _progress, _global);

        private static void AssertSameCorners(CharacterState _a, CharacterState _b) {
            Assert.That(Vector3.Distance(_a.BottomLeft, _b.BottomLeft), Is.LessThan(Tolerance));
            Assert.That(Vector3.Distance(_a.TopLeft, _b.TopLeft), Is.LessThan(Tolerance));
            Assert.That(Vector3.Distance(_a.TopRight, _b.TopRight), Is.LessThan(Tolerance));
            Assert.That(Vector3.Distance(_a.BottomRight, _b.BottomRight), Is.LessThan(Tolerance));
        }
        #endregion

        // ==================== TESTS ====================
        #region Transitions
        [Test]
        public void FadeIn_Progress0_AlphaZero_Progress1_Unchanged() {
            FadeTransition fade = Make<FadeTransition>("ease", "Linear");
            Assert.AreEqual(EffectKind.Appear, fade.Kind);

            CharacterState start = Quad();
            fade.Apply(ref start, At(0f));
            Assert.AreEqual(0, start.ColorBL.a);
            Assert.AreEqual(0, start.ColorTR.a);

            CharacterState end = Quad();
            fade.Apply(ref end, At(1f));
            Assert.AreEqual(255, end.ColorBL.a);
            Assert.AreEqual(255, end.ColorBR.a);
        }

        [Test]
        public void FadeOut_IsReverse() {
            FadeTransition fade = Make<FadeTransition>("ease", "Linear", "direction", "Out");
            Assert.AreEqual(EffectKind.Disappear, fade.Kind);

            CharacterState start = Quad();
            fade.Apply(ref start, At(0f));
            Assert.AreEqual(255, start.ColorBL.a);

            CharacterState end = Quad();
            fade.Apply(ref end, At(1f));
            Assert.AreEqual(0, end.ColorBL.a);
        }

        [Test]
        public void ScaleIn_Half_CornersHalfwayFromCenter() {
            ScaleTransition scale = Make<ScaleTransition>("ease", "Linear", "from", "0");
            CharacterState state = Quad();
            scale.Apply(ref state, At(0.5f));

            Assert.That(Vector3.Distance(state.BottomLeft, new Vector3(0.5f, 0.5f, 0f)), Is.LessThan(Tolerance));
            Assert.That(Vector3.Distance(state.TopRight, new Vector3(1.5f, 1.5f, 0f)), Is.LessThan(Tolerance));
            Assert.That(Vector3.Distance(state.Center, new Vector3(1f, 1f, 0f)), Is.LessThan(Tolerance));
        }

        [Test]
        public void OffsetIn_Progress0_ShiftedBySizeTimesOffset() {
            OffsetTransition offset = Make<OffsetTransition>("ease", "Linear", "offset", "0.5,-1");
            CharacterState state = Quad();
            offset.Apply(ref state, At(0f));

            // Size 2 -> shift of (1, -2, 0)
            Assert.That(Vector3.Distance(state.BottomLeft, new Vector3(1f, -2f, 0f)), Is.LessThan(Tolerance));

            CharacterState end = Quad();
            offset.Apply(ref end, At(1f));
            AssertSameCorners(Quad(), end);
        }
        #endregion

        #region Continuous
        [Test]
        public void Shake_BoundedByAmplitude() {
            ShakeEffect shake = Make<ShakeEffect>("amplitude", "0.05", "frequency", "20");
            Assert.AreEqual(EffectKind.Continuous, shake.Kind);

            float limit = 0.05f * 2f + Tolerance;
            for (int i = 0; i < 200; i++) {
                CharacterState state = Quad(i % 7);
                shake.Apply(ref state, At(0f, i * 0.037f));
                Vector3 delta = state.BottomLeft - Quad().BottomLeft;
                Assert.LessOrEqual(Mathf.Abs(delta.x), limit);
                Assert.LessOrEqual(Mathf.Abs(delta.y), limit);
            }
        }

        [Test]
        public void Shake_Deterministic() {
            ShakeEffect shake = Make<ShakeEffect>();
            CharacterState a = Quad(3);
            CharacterState b = Quad(3);
            shake.Apply(ref a, At(0f, 1.234f));
            shake.Apply(ref b, At(0f, 1.234f));
            AssertSameCorners(a, b);
        }

        [Test]
        public void Wave_DifferentIndex_DifferentPhase() {
            WaveEffect wave = Make<WaveEffect>();
            CharacterState a = Quad(0);
            CharacterState b = Quad(1);
            wave.Apply(ref a, At(0f, 0.1f));
            wave.Apply(ref b, At(0f, 0.1f));
            Assert.That(Mathf.Abs(a.BottomLeft.y - b.BottomLeft.y), Is.GreaterThan(0.001f));
        }

        [Test]
        public void ColorPulse_KeepsAlpha() {
            ColorPulseEffect pulse = Make<ColorPulseEffect>("color", "#FF0000");
            for (int i = 0; i < 20; i++) {
                CharacterState state = Quad();
                state.ColorBL.a = 120;
                state.ColorTR.a = 77;
                pulse.Apply(ref state, At(0f, i * 0.1f));
                Assert.AreEqual(120, state.ColorBL.a);
                Assert.AreEqual(77, state.ColorTR.a);
            }
        }
        #endregion

        #region OneShot
        [Test]
        public void PunchScale_EndsAtRest() {
            PunchScaleEffect punch = Make<PunchScaleEffect>();
            Assert.AreEqual(EffectKind.OneShot, punch.Kind);

            CharacterState start = Quad();
            punch.Apply(ref start, At(0f));
            Assert.Greater(Vector3.Distance(start.BottomLeft, Quad().BottomLeft), 0.01f);

            CharacterState end = Quad();
            punch.Apply(ref end, At(1f));
            AssertSameCorners(Quad(), end);
        }

        [Test]
        public void ShakeOnce_EndsAtRest() {
            ShakeOnceEffect shake = Make<ShakeOnceEffect>();
            CharacterState end = Quad(2);
            shake.Apply(ref end, At(1f, 0.7f));
            AssertSameCorners(Quad(2), end);
        }
        #endregion

        #region Allocations
        [Test]
        public void Apply_NoAllocations() {
            TextEffect[] effects = {
                new FadeTransition(), new ScaleTransition(), new OffsetTransition(), new ShakeEffect(), new WaveEffect(),
                new AlphaPulseEffect(), new ColorPulseEffect(), new ShakeOnceEffect(), new PunchScaleEffect()
            };

            // Warm up so lazy initialization is not counted
            CharacterState warm = Quad();
            for (int e = 0; e < effects.Length; e++) effects[e].Apply(ref warm, At(0.5f, 1f));

            using ProfilerRecorder recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 1, ProfilerRecorderOptions.Default | ProfilerRecorderOptions.CollectOnlyOnCurrentThread);

            // Positive control: the measure must see a known allocation
            object[] sink = new object[1];
            long controlStart = recorder.CurrentValue;
            for (int i = 0; i < 1000; i++) sink = new object[1];
            long controlCount = recorder.CurrentValue - controlStart;
            Assert.Greater(controlCount, 0, "Positive control must register allocations, otherwise the measure is useless");
            Assert.IsNotNull(sink);

            CharacterState state = Quad(1);
            long start = recorder.CurrentValue;
            for (int i = 0; i < 1000; i++) {
                EffectTime time = At((i % 100) / 100f, i * 0.016f);
                for (int e = 0; e < effects.Length; e++) effects[e].Apply(ref state, time);
            }

            long allocated = recorder.CurrentValue - start;
            Assert.AreEqual(0, allocated, "Effects must not allocate");
        }
        #endregion

        #region Clone
        [Test]
        public void CloneWithOverrides_DoesNotTouchOriginal() {
            WaveEffect original = new();
            List<TextIssue> issues = new();
            TextEffect clone = original.CloneWithOverrides(new List<EffectParam> { new("amplitude", "5") }, issues);

            Assert.AreNotSame(original, clone);
            Assert.IsEmpty(issues);

            CharacterState a = Quad();
            CharacterState b = Quad();
            original.Apply(ref a, At(0f, 0.1f));
            clone.Apply(ref b, At(0f, 0.1f));
            Assert.Greater(Mathf.Abs(b.BottomLeft.y), Mathf.Abs(a.BottomLeft.y) * 10f);
        }

        [Test]
        public void CloneWithOverrides_NoOverrides_ReturnsSameInstance() {
            WaveEffect original = new();
            Assert.AreSame(original, original.CloneWithOverrides(null, null));
            Assert.AreSame(original, original.CloneWithOverrides(new List<EffectParam>(), null));
        }

        [Test]
        public void CloneWithOverrides_BadParam_ReportsIssue() {
            WaveEffect original = new();
            List<TextIssue> issues = new();
            original.CloneWithOverrides(new List<EffectParam> { new("nope", "1") }, issues);
            Assert.AreEqual(1, issues.Count);
            Assert.AreEqual(IssueSeverity.Warning, issues[0].Severity);
        }
        #endregion
    }
}
