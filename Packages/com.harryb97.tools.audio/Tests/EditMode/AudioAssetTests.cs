using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace HarryTools.Audio.Tests {
    public class AudioAssetTests {
        // ==================== VARIABLES ===================
        private readonly List<Object> created = new List<Object>();

        private sealed class TestExtension : IAudioAssetExtension { }
        private sealed class OtherExtension : IAudioAssetExtension { }

        // ==================== SETUP ===================
        [TearDown]
        public void TearDown() {
            for (int _i = 0; _i < created.Count; _i++) Object.DestroyImmediate(created[_i]);
            created.Clear();
        }

        private T Make<T>() where T : ScriptableObject {
            var _instance = ScriptableObject.CreateInstance<T>();
            created.Add(_instance);
            return _instance;
        }

        // ==================== TESTS ===================
        [Test]
        public void GetSpatial_ProfileOverridesInline() {
            var _sound = Make<SoundAsset>();
            _sound.SetSpatial(SpatialSettings.Default2D);
            Assert.AreEqual(0f, _sound.GetSpatial().SpatialBlend);

            var _profile = Make<SpatialProfile>();
            _profile.Settings = SpatialSettings.Default3D;
            _sound.SetSpatialProfile(_profile);
            Assert.AreEqual(1f, _sound.GetSpatial().SpatialBlend);
        }

        [Test]
        public void GetExtension_ReturnsMatchingType() {
            var _sound = Make<SoundAsset>();
            var _ext = new TestExtension();
            _sound.AddExtension(_ext);
            Assert.AreSame(_ext, _sound.GetExtension<TestExtension>());
            Assert.IsNull(_sound.GetExtension<OtherExtension>());

            var _speaker = Make<SpeakerAsset>();
            _speaker.AddExtension(_ext);
            Assert.AreSame(_ext, _speaker.GetExtension<TestExtension>());
            Assert.IsNull(_speaker.GetExtension<OtherExtension>());
        }

        [Test]
        public void Defaults_PerType() {
            var _sound = Make<SoundAsset>();
            Assert.AreEqual(AudioBus.SFX, _sound.Bus);
            Assert.AreEqual(50, _sound.Priority);
            Assert.AreEqual(Vector2.one, _sound.VolumeRange);
            Assert.AreEqual(Vector2.one, _sound.PitchRange);

            var _music = Make<MusicAsset>();
            Assert.AreEqual(AudioBus.Music, _music.Bus);
            Assert.IsTrue(_music.Loop);
            Assert.AreEqual(100, _music.Priority);

            var _voice = Make<VoiceAsset>();
            Assert.AreEqual(AudioBus.Voice, _voice.Bus);
            Assert.AreEqual(90, _voice.Priority);
        }

        [Test]
        public void ApplyTo_CustomRolloffWithoutKeys_FallsBackToLogarithmic() {
            var _go = new GameObject("ApplyTo Test");
            try {
                var _source = _go.AddComponent<AudioSource>();
                SpatialSettings _spatial = SpatialSettings.Default3D;
                _spatial.Rolloff = AudioRolloffMode.Custom;
                _spatial.CustomRolloff = new AnimationCurve();
                _spatial.ApplyTo(_source);
                Assert.AreEqual(AudioRolloffMode.Logarithmic, _source.rolloffMode);

                _spatial.CustomRolloff = null;
                _spatial.ApplyTo(_source);
                Assert.AreEqual(AudioRolloffMode.Logarithmic, _source.rolloffMode);

                _spatial.CustomRolloff = AnimationCurve.Linear(0f, 1f, 1f, 0f);
                _spatial.ApplyTo(_source);
                Assert.AreEqual(AudioRolloffMode.Custom, _source.rolloffMode);
            } finally {
                Object.DestroyImmediate(_go);
            }
        }

        [Test]
        public void NewBusConfig_DefaultVolumeIsOne() {
            Assert.AreEqual(1f, new BusConfig().DefaultVolume);
        }

        [Test]
        public void Refs_NullAndEmptyId_AreEqual() {
            Assert.IsTrue(default(SoundRef) == new SoundRef(""));
            Assert.AreEqual(default(SoundRef).GetHashCode(), new SoundRef("").GetHashCode());
            Assert.IsFalse(new SoundRef("").IsValid);
            Assert.IsTrue(default(MusicRef) == new MusicRef(""));
            Assert.AreEqual(default(MusicRef).GetHashCode(), new MusicRef("").GetHashCode());
            Assert.IsTrue(default(VoiceRef) == new VoiceRef(""));
            Assert.AreEqual(default(VoiceRef).GetHashCode(), new VoiceRef("").GetHashCode());
            Assert.IsTrue(default(ConversationRef) == new ConversationRef(""));
            Assert.AreEqual(default(ConversationRef).GetHashCode(), new ConversationRef("").GetHashCode());
            Assert.IsFalse(new SoundRef("a") == new SoundRef(""));
        }
    }
}
