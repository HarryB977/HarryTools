using System.Collections.Generic;
using HarryTools.Audio.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HarryTools.Audio.Tests {
    public class AudioCatalogTests {
        // ==================== VARIABLES ===================
        private const string Folder = "Assets/HarryToolsAudioTests_Temp";
        private AudioCatalog catalog;

        // ==================== SETUP ===================
        [SetUp]
        public void SetUp() {
            DeleteFolder();
            AssetDatabase.CreateFolder("Assets", "HarryToolsAudioTests_Temp");
            catalog = ScriptableObject.CreateInstance<AudioCatalog>();
        }

        [TearDown]
        public void TearDown() {
            if (catalog != null) Object.DestroyImmediate(catalog);
            DeleteFolder();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown() => DeleteFolder();

        private static void DeleteFolder() {
            if (AssetDatabase.IsValidFolder(Folder)) AssetDatabase.DeleteAsset(Folder);
            AssetDatabase.Refresh();
        }

        private static T Create<T>(string _name, string _subfolder = null) where T : AudioCatalogItem {
            string _folder = Folder;
            if (_subfolder != null) {
                if (!AssetDatabase.IsValidFolder(Folder + "/" + _subfolder)) AssetDatabase.CreateFolder(Folder, _subfolder);
                _folder = Folder + "/" + _subfolder;
            }
            var _asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(_asset, _folder + "/" + _name + ".asset");
            return _asset;
        }

        private static string GuidOf(Object _asset) => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(_asset));

        // ==================== TESTS ===================
        [Test]
        public void NewAsset_GetsIdEqualToGuid() {
            var _sound = Create<SoundAsset>("A");
            Assert.IsTrue(AudioIdAssigner.EnsureId(_sound));
            Assert.AreEqual(GuidOf(_sound), _sound.Id);
            Assert.IsFalse(AudioIdAssigner.EnsureId(_sound));
        }

        [Test]
        public void RenamedAsset_KeepsId() {
            var _sound = Create<SoundAsset>("A");
            AudioIdAssigner.EnsureId(_sound);
            string _id = _sound.Id;
            Assert.IsEmpty(AssetDatabase.RenameAsset(Folder + "/A.asset", "B"));
            AudioIdAssigner.EnsureId(_sound);
            Assert.AreEqual(_id, _sound.Id);
        }

        [Test]
        public void MovedAsset_KeepsId() {
            var _sound = Create<SoundAsset>("A");
            AudioIdAssigner.EnsureId(_sound);
            string _id = _sound.Id;
            AssetDatabase.CreateFolder(Folder, "Sub");
            Assert.IsEmpty(AssetDatabase.MoveAsset(Folder + "/A.asset", Folder + "/Sub/A.asset"));
            AudioIdAssigner.EnsureId(_sound);
            Assert.AreEqual(_id, _sound.Id);
        }

        [Test]
        public void DuplicatedAsset_GetsNewId_OriginalKeepsId() {
            var _original = Create<SoundAsset>("A");
            AudioIdAssigner.EnsureId(_original);
            string _originalId = _original.Id;
            AssetDatabase.SaveAssets();
            Assert.IsTrue(AssetDatabase.CopyAsset(Folder + "/A.asset", Folder + "/A copy.asset"));
            var _copy = AssetDatabase.LoadAssetAtPath<SoundAsset>(Folder + "/A copy.asset");
            Assert.AreEqual(_originalId, _copy.Id, "precondition: copy carries the serialized id");

            AudioCatalogMaintainer.Rebuild(catalog);

            Assert.AreEqual(_originalId, _original.Id);
            Assert.AreEqual(GuidOf(_copy), _copy.Id);
            Assert.AreNotEqual(_original.Id, _copy.Id);
        }

        [Test]
        public void Rebuild_ListsAllItems_SortedByPath() {
            Create<MusicAsset>("Z");
            Create<SoundAsset>("B", "Sub");
            Create<VoiceAsset>("A");
            AudioCatalogMaintainer.Rebuild(catalog);

            var _paths = new List<string>();
            for (int _i = 0; _i < catalog.Items.Count; _i++) {
                string _path = AssetDatabase.GetAssetPath(catalog.Items[_i]);
                if (_path.StartsWith(Folder + "/")) _paths.Add(_path);
            }
            Assert.AreEqual(3, _paths.Count);
            var _sorted = new List<string>(_paths);
            _sorted.Sort(System.StringComparer.Ordinal);
            CollectionAssert.AreEqual(_sorted, _paths);
        }

        [Test]
        public void TryGet_ById_ReturnsItem() {
            var _sound = Create<SoundAsset>("A");
            AudioCatalogMaintainer.Rebuild(catalog);
            Assert.IsTrue(catalog.TryGet(_sound.Id, out AudioCatalogItem _item));
            Assert.AreSame(_sound, _item);
            Assert.IsTrue(catalog.TryGet<SoundAsset>(_sound.Id, out var _typed));
            Assert.AreSame(_sound, _typed);
            Assert.IsFalse(catalog.TryGet("missing", out _item));
        }

        [Test]
        public void TryGet_WrongType_ReturnsFalse() {
            var _sound = Create<SoundAsset>("A");
            AudioCatalogMaintainer.Rebuild(catalog);
            Assert.IsFalse(catalog.TryGet<MusicAsset>(_sound.Id, out var _music));
            Assert.IsNull(_music);
        }
    }
}
