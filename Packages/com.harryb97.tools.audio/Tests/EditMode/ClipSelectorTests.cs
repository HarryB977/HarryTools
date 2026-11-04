using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace HarryTools.Audio.Tests {
    public class ClipSelectorTests {
        // ==================== VARIABLES ===================
        private readonly List<Object> created = new List<Object>();

        // ==================== SETUP ===================
        [TearDown]
        public void TearDown() {
            for (int _i = 0; _i < created.Count; _i++) Object.DestroyImmediate(created[_i]);
            created.Clear();
        }

        // ==================== HELPERS ===================
        private AudioClip MakeClip(string _name) {
            var _clip = AudioClip.Create(_name, 441, 1, 44100, false);
            created.Add(_clip);
            return _clip;
        }

        private ClipSelector MakeSelector(ClipSelection _mode, params ClipEntry[] _entries) {
            var _asset = ScriptableObject.CreateInstance<SoundAsset>();
            created.Add(_asset);
            _asset.SetClips(_entries);
            _asset.SetSelection(_mode);
            return new ClipSelector(_asset, new System.Random(1));
        }

        // ==================== TESTS ===================
        [Test]
        public void Random_RespectsWeights() {
            var _a = MakeClip("a");
            var _b = MakeClip("b");
            var _selector = MakeSelector(ClipSelection.Random, new ClipEntry(_a, 1f), new ClipEntry(_b, 0f));
            for (int _i = 0; _i < 100; _i++) Assert.AreSame(_a, _selector.Next());
        }

        [Test]
        public void RandomNoRepeat_NeverRepeatsConsecutively() {
            var _selector = MakeSelector(ClipSelection.RandomNoRepeat,
                new ClipEntry(MakeClip("a")), new ClipEntry(MakeClip("b")), new ClipEntry(MakeClip("c")));
            AudioClip _previous = null;
            for (int _i = 0; _i < 200; _i++) {
                var _clip = _selector.Next();
                Assert.AreNotSame(_previous, _clip);
                _previous = _clip;
            }
        }

        [Test]
        public void Shuffle_PlaysAllBeforeRepeating() {
            var _selector = MakeSelector(ClipSelection.Shuffle,
                new ClipEntry(MakeClip("a")), new ClipEntry(MakeClip("b")),
                new ClipEntry(MakeClip("c")), new ClipEntry(MakeClip("d")));
            var _seen = new HashSet<AudioClip>();
            for (int _i = 0; _i < 4; _i++) _seen.Add(_selector.Next());
            Assert.AreEqual(4, _seen.Count);
        }

        [Test]
        public void Sequence_CyclesInOrder() {
            var _a = MakeClip("a");
            var _b = MakeClip("b");
            var _c = MakeClip("c");
            var _selector = MakeSelector(ClipSelection.Sequence, new ClipEntry(_a), new ClipEntry(_b), new ClipEntry(_c));
            Assert.AreSame(_a, _selector.Next());
            Assert.AreSame(_b, _selector.Next());
            Assert.AreSame(_c, _selector.Next());
            Assert.AreSame(_a, _selector.Next());
        }

        [Test]
        public void SingleClip_NoRepeat_ReturnsSameClip() {
            var _a = MakeClip("a");
            var _selector = MakeSelector(ClipSelection.RandomNoRepeat, new ClipEntry(_a));
            for (int _i = 0; _i < 10; _i++) Assert.AreSame(_a, _selector.Next());
        }

        [Test]
        public void NoValidClips_ReturnsNull() {
            Assert.IsNull(MakeSelector(ClipSelection.Random).Next());
            Assert.IsNull(MakeSelector(ClipSelection.Shuffle, new ClipEntry(null), new ClipEntry(null)).Next());
        }
    }
}
